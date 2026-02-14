using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using NAudio.Wave;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
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
            var parsed = _ctx.ClientAdapter.ParseMessage(rawMessage);

            switch (parsed.Type)
            {
                case RealtimeAiClientMessageType.Start:
                    await (_ctx.Options.OnClientStartAsync?.Invoke(_ctx.SessionId, parsed.Metadata ?? new()) ?? Task.CompletedTask).ConfigureAwait(false);
                    break;
                case RealtimeAiClientMessageType.Stop:
                    await (_ctx.Options.OnClientStopAsync?.Invoke(_ctx.SessionId) ?? Task.CompletedTask).ConfigureAwait(false);
                    break;
                case RealtimeAiClientMessageType.Audio:
                    await HandleClientAudioAsync(parsed.Payload).ConfigureAwait(false);
                    break;
                case RealtimeAiClientMessageType.Image:
                    await HandleClientImageAsync(parsed.Payload).ConfigureAwait(false);
                    break;
                case RealtimeAiClientMessageType.Text:
                    await HandleClientTextAsync(parsed.Payload).ConfigureAwait(false);
                    break;
                default:
                    Log.Warning("[RealtimeAi] Unknown client message type, SessionId: {SessionId}", _ctx.SessionId);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RealtimeAi] Failed to process client message, SessionId: {SessionId}", _ctx.SessionId);
        }
    }

    private async Task HandleClientAudioAsync(string base64Payload)
    {
        var providerBase64 = await TranscodeAudioAsync(base64Payload, AudioSource.Client).ConfigureAwait(false);

        if (_ctx.IsClientAudioToProviderSuspended) return;

        await SendToProviderAsync(_ctx.ProviderAdapter.BuildAudioAppendMessage(new RealtimeAiWssAudioData { Base64Payload = providerBase64 })).ConfigureAwait(false);
    }

    private async Task HandleClientImageAsync(string base64Payload)
    {
        await SendToProviderAsync(_ctx.ProviderAdapter.BuildAudioAppendMessage(new RealtimeAiWssAudioData
        {
            Base64Payload = base64Payload,
            CustomProperties = new Dictionary<string, object> { { "image", base64Payload } }
        })).ConfigureAwait(false);
    }

    private async Task HandleClientTextAsync(string text)
    {
        await SendTextToProviderAsync(text).ConfigureAwait(false);
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
            () => { _inactivityTimerManager.StopTimer(_ctx.SessionId); return Task.CompletedTask; }, "stop inactivity timer");

        await SafeExecuteAsync(
            () => _ctx.Options?.OnSessionEndedAsync?.Invoke(_ctx.SessionId) ?? Task.CompletedTask, "invoke OnSessionEndedAsync");

        await SafeExecuteAsync(HandleRecordingAsync, "handle recording");
        await SafeExecuteAsync(HandleTranscriptionsAsync, "handle transcriptions");
    }

    private enum AudioSource { Client, Provider }

    /// <summary>
    /// Central audio pipeline: decode → record → convert codec → re-encode.
    /// Resolves source/target codecs from AudioSource; decodes at most once.
    /// </summary>
    private async Task<string> TranscodeAudioAsync(string base64Input, AudioSource source)
    {
        var clientCodec = _ctx.ClientAdapter.NativeAudioCodec;
        var providerCodec = _ctx.ProviderAdapter.GetPreferredCodec(clientCodec);
        var (sourceCodec, targetCodec) = source == AudioSource.Client ? (clientCodec, providerCodec) : (providerCodec, clientCodec);

        var rawBytes = await RecordAudioIfRequiredAsync(base64Input, sourceCodec, source).ConfigureAwait(false);

        if (sourceCodec == targetCodec) return base64Input;

        rawBytes ??= Convert.FromBase64String(base64Input);
        
        return Convert.ToBase64String(AudioCodecConverter.Convert(rawBytes, sourceCodec, targetCodec));
    }

    /// <summary>
    /// Decides whether recording should happen, decodes and writes to buffer if so.
    /// Returns decoded bytes for reuse by codec conversion, or null if no decode occurred.
    /// </summary>
    private async Task<byte[]> RecordAudioIfRequiredAsync(string base64Input, RealtimeAiAudioCodec sourceCodec, AudioSource source)
    {
        if (!_ctx.Options.EnableRecording) return null;
        if (source == AudioSource.Client && _ctx.IsAiSpeaking) return null;

        var rawBytes = Convert.FromBase64String(base64Input);
        
        await WriteToAudioBufferAsync(rawBytes, sourceCodec).ConfigureAwait(false);
        
        return rawBytes;
    }

    private async Task WriteToAudioBufferAsync(byte[] data, RealtimeAiAudioCodec sourceCodec)
    {
        if (!_ctx.Options.EnableRecording || _ctx.AudioBuffer is not { CanWrite: true }) return;

        var pcmData = AudioCodecConverter.ConvertForRecording(data, sourceCodec);

        await _ctx.BufferLock.WaitAsync(_ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_ctx.AudioBuffer is { CanWrite: true })
                await _ctx.AudioBuffer.WriteAsync(pcmData).ConfigureAwait(false);
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
            _ctx.AudioBuffer = null;
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
