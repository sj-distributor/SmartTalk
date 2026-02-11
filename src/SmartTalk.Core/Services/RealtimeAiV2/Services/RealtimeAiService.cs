using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NAudio.Wave;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Dto.RealtimeAi;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public interface IRealtimeAiService : IScopedDependency
{
    Task StartAsync(RealtimeSessionOptions options, CancellationToken cancellationToken);
}

public partial class RealtimeAiService : IRealtimeAiService
{
    private RealtimeAiSessionContext _ctx;

    private readonly IRealtimeAiSwitcher _realtimeAiSwitcher;
    private readonly IInactivityTimerManager _inactivityTimerManager;

    public RealtimeAiService(
        IRealtimeAiSwitcher realtimeAiSwitcher,
        IInactivityTimerManager inactivityTimerManager)
    {
        _realtimeAiSwitcher = realtimeAiSwitcher;
        _inactivityTimerManager = inactivityTimerManager;
    }

    public async Task StartAsync(RealtimeSessionOptions options, CancellationToken cancellationToken)
    {
        BuildSessionContext(options);

        Log.Information("[RealtimeAi] Session initialized, Context: {@Context}", _ctx);

        try
        {
            await ConnectToProviderAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisconnectFromProviderAsync("Session start failed").ConfigureAwait(false);
            throw;
        }

        await ReceiveFromWebSocketClientAsync(cancellationToken).ConfigureAwait(false);
    }
    
    private async Task ReceiveFromWebSocketClientAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var gracefulClose = false;
        var ms = new MemoryStream();

        try
        {
            while (_ctx.WebSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                ms.SetLength(0);

                do
                {
                    result = await _ctx.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        gracefulClose = true;

                        Log.Information("[RealtimeAi] Client sent close frame, SessionId: {SessionId}, AssistantId: {AssistantId}",
                            _ctx.SessionId, _ctx.Options.ConnectionProfile.ProfileId);

                        await DisconnectFromProviderAsync("Client disconnected").ConfigureAwait(false);
                        await _ctx.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client acknowledges close", CancellationToken.None);

                        Log.Information("[RealtimeAi] Session closed gracefully, SessionId: {SessionId}, AssistantId: {AssistantId}",
                            _ctx.SessionId, _ctx.Options.ConnectionProfile.ProfileId);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var rawMessage = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);

                Log.Debug("[RealtimeAi] Received client message, SessionId: {SessionId}, AssistantId: {AssistantId}, Message: {Message}",
                    _ctx.SessionId, _ctx.Options.ConnectionProfile.ProfileId, rawMessage);

                try
                {
                    using var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(rawMessage);
                    var payload = jsonDocument?.RootElement.GetProperty("media").GetProperty("payload").GetString();

                    if (!string.IsNullOrWhiteSpace(payload))
                    {
                        var audioBytes = Convert.FromBase64String(payload);

                        if (!_ctx.IsAiSpeaking)
                            await WriteToAudioBufferAsync(audioBytes).ConfigureAwait(false);

                        await SendAudioToProviderAsync(new RealtimeAiWssAudioData
                        {
                            Base64Payload = payload,
                            CustomProperties = new Dictionary<string, object>
                            {
                                { nameof(RealtimeSessionOptions.InputFormat), _ctx.Options.InputFormat }
                            }
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        Log.Warning("[RealtimeAi] Received empty payload, SessionId: {SessionId}, AssistantId: {AssistantId}",
                            _ctx.SessionId, _ctx.Options.ConnectionProfile.ProfileId);
                    }
                }
                catch (JsonException jsonEx)
                {
                    Log.Error(jsonEx, "[RealtimeAi] Failed to parse client message, SessionId: {SessionId}, AssistantId: {AssistantId}, RawMessage: {RawMessage}",
                        _ctx.SessionId, _ctx.Options.ConnectionProfile.ProfileId, rawMessage);
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Error(ex, "[RealtimeAi] WebSocket receive error, SessionId: {SessionId}, AssistantId: {AssistantId}, WebSocketState: {WebSocketState}",
                _ctx.SessionId, _ctx.Options?.ConnectionProfile?.ProfileId, GetWebSocketStateSafe());
        }
        catch (OperationCanceledException)
        {
            Log.Warning("[RealtimeAi] WebSocket receive cancelled, SessionId: {SessionId}, AssistantId: {AssistantId}",
                _ctx.SessionId, _ctx.Options?.ConnectionProfile?.ProfileId);
        }
        finally
        {
            if (!gracefulClose)
            {
                Log.Warning("[RealtimeAi] Client disconnected abnormally, cleaning up, SessionId: {SessionId}, AssistantId: {AssistantId}, WebSocketState: {WebSocketState}",
                    _ctx.SessionId, _ctx.Options?.ConnectionProfile?.ProfileId, GetWebSocketStateSafe());

                try
                {
                    await DisconnectFromProviderAsync("Client disconnected abnormally").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[RealtimeAi] Failed to disconnect from provider during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}",
                        _ctx.SessionId, _ctx.Options?.ConnectionProfile?.ProfileId);
                }

                try
                {
                    _inactivityTimerManager.StopTimer(_ctx.StreamSid);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[RealtimeAi] Failed to stop inactivity timer during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}, StreamSid: {StreamSid}",
                        _ctx.SessionId, _ctx.Options?.ConnectionProfile?.ProfileId, _ctx.StreamSid);
                }
            }

            try
            {
                await HandleRecordingAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RealtimeAi] Failed to handle recording during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}",
                    _ctx.SessionId, _ctx.Options?.ConnectionProfile?.ProfileId);
            }

            try
            {
                if (_ctx.Options?.OnSessionEndedAsync != null)
                    await _ctx.Options.OnSessionEndedAsync(_ctx.SessionId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RealtimeAi] Failed to invoke OnSessionEndedAsync during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}",
                    _ctx.SessionId, _ctx.Options?.ConnectionProfile?.ProfileId);
            }

            try
            {
                await HandleTranscriptionsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RealtimeAi] Failed to handle transcriptions during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}",
                    _ctx.SessionId, _ctx.Options?.ConnectionProfile?.ProfileId);
            }

            ms.Dispose();
        }
    }

    private async Task HandleTranscriptionsAsync()
    {
        if (_ctx.Options.OnTranscriptionsCompletedAsync == null || _ctx.Transcriptions.IsEmpty) return;

        var transcriptions = _ctx.Transcriptions.Select(t => (t.Speaker, t.Text)).ToList();
        await _ctx.Options.OnTranscriptionsCompletedAsync(_ctx.SessionId, transcriptions).ConfigureAwait(false);
    }

    private async Task WriteToAudioBufferAsync(byte[] data)
    {
        if (!_ctx.Options.EnableRecording || _ctx.AudioBuffer is not { CanWrite: true }) return;

        await _ctx.BufferLock.WaitAsync().ConfigureAwait(false);
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

    private async Task SendToClientAsync(object payload)
    {
        if (_ctx.WebSocket is not { State: WebSocketState.Open }) return;

        await _ctx.WsSendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_ctx.WebSocket is not { State: WebSocketState.Open }) return;

            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
            await _ctx.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (WebSocketException ex)
        {
            Log.Warning(ex, "[RealtimeAi] Failed to send message to client, SessionId: {SessionId}, AssistantId: {AssistantId}, WebSocketState: {WebSocketState}",
                _ctx.SessionId, _ctx.Options?.ConnectionProfile?.ProfileId, _ctx.WebSocket?.State);
        }
        finally
        {
            _ctx.WsSendLock.Release();
        }
    }

    private void StartInactivityTimer(int seconds, string followUpMessage)
    {
        _inactivityTimerManager.StartTimer(_ctx.StreamSid, TimeSpan.FromSeconds(seconds), async () =>
        {
            Log.Warning("[RealtimeAi] Idle follow-up triggered, SessionId: {SessionId}, AssistantId: {AssistantId}, TimeoutSeconds: {TimeoutSeconds}",
                _ctx.SessionId, _ctx.Options.ConnectionProfile.ProfileId, seconds);

            await SendTextToProviderAsync(followUpMessage);
        });
    }

    private void StopInactivityTimer()
    {
        _inactivityTimerManager.StopTimer(_ctx.StreamSid);
    }

    private string GetWebSocketStateSafe()
    {
        try { return _ctx.WebSocket?.State.ToString() ?? "null"; }
        catch { return "unknown"; }
    }
}