using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NAudio.Wave;
using Serilog;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    private static readonly (string Property, string Type, Func<JsonElement, string> Extract)[] ClientMessageParsers =
    [
        ("media", "audio", e => e.GetProperty("payload").GetString()),
        ("text",  "text",  e => e.GetString())
    ];
    
    private async Task OrchestrateSessionAsync()
    {
        var clientIsClose = false;
        var buffer = ArrayPool<byte>.Shared.Rent(8192);

        try
        {
            while (_ctx.WebSocket.State == WebSocketState.Open)
            {
                (var message, clientIsClose) = await ReadClientMessageAsync(buffer).ConfigureAwait(false);

                if (clientIsClose) break;

                if (message != null) await ProcessClientMessageAsync(message).ConfigureAwait(false);
            }
        }
        catch (WebSocketException ex)
        {
            Log.Error(ex, "[RealtimeAi] WebSocket error, SessionId: {SessionId}, WebSocketState: {WebSocketState}", _ctx.SessionId, GetWebSocketStateSafe());
        }
        catch (OperationCanceledException)
        {
            Log.Warning("[RealtimeAi] WebSocket cancelled, SessionId: {SessionId}", _ctx.SessionId);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            
            await CleanupSessionAsync(clientIsClose).ConfigureAwait(false);
        }
    }

    private async Task<(string Message, bool ClientIsClose)> ReadClientMessageAsync(byte[] buffer)
    {
        using var ms = new MemoryStream();

        ValueWebSocketReceiveResult result;

        do
        {
            result = await _ctx.WebSocket.ReceiveAsync(buffer.AsMemory(), _ctx.SessionCts.Token).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close) return (null, true);

            ms.Write(buffer, 0, result.Count);
            
        } while (!result.EndOfMessage);

        return (Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length), false);
    }

    private async Task ProcessClientMessageAsync(string rawMessage)
    {
        try
        {
            var (type, payload) = ParseClientMessage(rawMessage);

            switch (type)
            {
                case "audio":
                    await HandleClientAudioAsync(payload).ConfigureAwait(false);
                    break;
                case "text":
                    await HandleClientTextAsync(payload).ConfigureAwait(false);
                    break;
                default:
                    Log.Warning("[RealtimeAi] Unknown client message type, SessionId: {SessionId}", _ctx.SessionId);
                    break;
            }
        }
        catch (JsonException jsonEx)
        {
            Log.Error(jsonEx, "[RealtimeAi] Failed to parse client message, SessionId: {SessionId}", _ctx.SessionId);
        }
    }

    private static (string Type, string Payload) ParseClientMessage(string rawMessage)
    {
        using var doc = JsonDocument.Parse(rawMessage);
        
        var root = doc.RootElement;

        foreach (var (property, type, extract) in ClientMessageParsers)
        {
            if (!root.TryGetProperty(property, out var element)) continue;
            
            var payload = extract(element);
            
            if (!string.IsNullOrWhiteSpace(payload)) return (type, payload);
        }

        return (null, null);
    }

    private async Task HandleClientTextAsync(string text)
    {
        await SendTextToProviderAsync(text).ConfigureAwait(false);
    }

    private async Task HandleClientAudioAsync(string base64Payload)
    {
        if (!_ctx.IsAiSpeaking)
        {
            var audioBytes = Convert.FromBase64String(base64Payload);
            await WriteToAudioBufferAsync(audioBytes).ConfigureAwait(false);
        }

        await SendAudioToProviderAsync(new RealtimeAiWssAudioData
        {
            Base64Payload = base64Payload,
            CustomProperties = new Dictionary<string, object>
            {
                { nameof(RealtimeSessionOptions.InputFormat), _ctx.Options.InputFormat }
            }
        }).ConfigureAwait(false);
    }

    private async Task CleanupSessionAsync(bool clientIsClose)
    {
        if (clientIsClose)
            await SafeExecuteAsync(
                () => _ctx.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close acknowledged", CancellationToken.None), "acknowledge client close");
        else
            Log.Warning("[RealtimeAi] Client disconnected abnormally, SessionId: {SessionId}, WebSocketState: {WebSocketState}", _ctx.SessionId, GetWebSocketStateSafe());
        
        await SafeExecuteAsync(
            () => DisconnectFromProviderAsync(clientIsClose ? "Client disconnected" : "Client disconnected abnormally"), "disconnect from provider");

        await SafeExecuteAsync(
            () => { _inactivityTimerManager.StopTimer(_ctx.StreamSid); return Task.CompletedTask; }, "stop inactivity timer");

        await SafeExecuteAsync(async 
            () => { if (_ctx.Options?.OnSessionEndedAsync != null) await _ctx.Options.OnSessionEndedAsync(_ctx.SessionId).ConfigureAwait(false); }, "invoke OnSessionEndedAsync");

        await SafeExecuteAsync(HandleRecordingAsync, "handle recording");
        await SafeExecuteAsync(HandleTranscriptionsAsync, "handle transcriptions");
    }

    private async Task WriteToAudioBufferAsync(byte[] data)
    {
        if (!_ctx.Options.EnableRecording || _ctx.AudioBuffer is not { CanWrite: true }) return;

        await _ctx.BufferLock.WaitAsync(_ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_ctx.AudioBuffer is { CanWrite: true })
                await _ctx.AudioBuffer.WriteAsync(data).ConfigureAwait(false);
        }
        finally
        {
            _ctx.BufferLock.Release();
        }
    }

    private async Task HandleTranscriptionsAsync()
    {
        if (_ctx.Options.OnTranscriptionsCompletedAsync == null || _ctx.Transcriptions.IsEmpty) return;

        var transcriptions = _ctx.Transcriptions.Select(t => (t.Speaker, t.Text)).ToList();
        
        await _ctx.Options.OnTranscriptionsCompletedAsync(_ctx.SessionId, transcriptions).ConfigureAwait(false);
    }
    
    private async Task HandleRecordingAsync()
    {
        if (!_ctx.Options.EnableRecording || _ctx.Options.OnRecordingCompleteAsync == null) return;

        MemoryStream snapshot;

        await _ctx.BufferLock.WaitAsync().ConfigureAwait(false);
        try
        {
            snapshot = _ctx.AudioBuffer;
        }
        finally
        {
            _ctx.BufferLock.Release();
        }

        if (snapshot is not { CanRead: true } || snapshot.Length == 0) return;

        try
        {
            var waveFormat = new WaveFormat(24000, 16, 1);
            using var wavStream = new MemoryStream();

            await using (var writer = new WaveFileWriter(wavStream, waveFormat))
            {
                var rented = ArrayPool<byte>.Shared.Rent(64 * 1024);
                try
                {
                    if (snapshot.CanSeek) snapshot.Position = 0;
                    int read;
                    while ((read = await snapshot.ReadAsync(rented.AsMemory(0, rented.Length))) > 0)
                    {
                        writer.Write(rented, 0, read);
                    }
                    await writer.FlushAsync();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }

            await _ctx.Options.OnRecordingCompleteAsync(_ctx.SessionId, wavStream.ToArray()).ConfigureAwait(false);
        }
        finally
        {
            await snapshot.DisposeAsync();
        }
    }
    
    private async Task SafeExecuteAsync(Func<Task> action, string operationName)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RealtimeAi] Cleanup failed: {Operation}, SessionId: {SessionId}", operationName, _ctx.SessionId);
        }
    }
}
