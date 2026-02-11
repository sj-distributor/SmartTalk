using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NAudio.Wave;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.RealtimeAiV2.Wss;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public interface IRealtimeAiService : IScopedDependency
{
    Task StartAsync(RealtimeSessionOptions options, RealtimeSessionCallbacks callbacks, CancellationToken cancellationToken);
}

public class RealtimeAiService : IRealtimeAiService
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

    public async Task StartAsync(RealtimeSessionOptions options, RealtimeSessionCallbacks callbacks, CancellationToken cancellationToken)
    {
        BuildSessionContext(options, callbacks);

        Log.Information("[RealtimeAi] Session initialized, SessionId: {SessionId}, ProfileId: {ProfileId}, Provider: {Provider}, InputFormat: {InputFormat}, OutputFormat: {OutputFormat}, Region: {Region}",
            _ctx.SessionId, _ctx.Options.ConnectionProfile.ProfileId, _ctx.Options.ModelConfig.Provider, options.InputFormat, options.OutputFormat, options.Region);

        BuildConversationEngine();

        try
        {
            await _ctx.ConversationEngine.StartSessionAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisposeConversationEngineAsync().ConfigureAwait(false);
            throw;
        }

        await ReceiveFromWebSocketClientAsync(cancellationToken).ConfigureAwait(false);
    }

    private void BuildSessionContext(RealtimeSessionOptions options, RealtimeSessionCallbacks callbacks)
    {
        _ctx = new RealtimeAiSessionContext
        {
            Options = options ?? throw new ArgumentNullException(nameof(options)),
            Callbacks = callbacks ?? new RealtimeSessionCallbacks(),
            WebSocket = options.WebSocket
        };

        _ctx.Options.ModelConfig = _ctx.Options.ModelConfig ?? throw new ArgumentNullException(nameof(_ctx.Options.ModelConfig));
        _ctx.Options.ConnectionProfile = _ctx.Options.ConnectionProfile ?? throw new ArgumentNullException(nameof(_ctx.Options.ConnectionProfile));
        
        if (_ctx.Options.EnableRecording) _ctx.AudioBuffer = new MemoryStream();
    }
    
    private void BuildConversationEngine()
    {
        if (_ctx.ConversationEngine != null)
        {
            _ctx.ConversationEngine.SessionStatusChangedAsync -= OnSessionStatusChangedAsync;
            _ctx.ConversationEngine.AiAudioOutputReadyAsync -= OnAiAudioOutputReadyAsync;
            _ctx.ConversationEngine.AiDetectedUserSpeechAsync -= OnAiDetectedUserSpeechAsync;
            _ctx.ConversationEngine.AiTurnCompletedAsync -= OnAiTurnCompletedAsync;
            _ctx.ConversationEngine.ErrorOccurredAsync -= OnErrorOccurredAsync;
            _ctx.ConversationEngine.InputAudioTranscriptionCompletedAsync -= InputAudioTranscriptionCompletedAsync;
            _ctx.ConversationEngine.OutputAudioTranscriptionCompletedyAsync -= OutputAudioTranscriptionCompletedAsync;
        }

        var client = _realtimeAiSwitcher.WssClient(_ctx.Options.ModelConfig.Provider);
        var adapter = _realtimeAiSwitcher.ProviderAdapter(_ctx.Options.ModelConfig.Provider);

        _ctx.ConversationEngine = new RealtimeAiConversationEngine(adapter, client);
        _ctx.ConversationEngine.SessionStatusChangedAsync += OnSessionStatusChangedAsync;
        _ctx.ConversationEngine.AiAudioOutputReadyAsync += OnAiAudioOutputReadyAsync;
        _ctx.ConversationEngine.AiDetectedUserSpeechAsync += OnAiDetectedUserSpeechAsync;
        _ctx.ConversationEngine.AiTurnCompletedAsync += OnAiTurnCompletedAsync;
        _ctx.ConversationEngine.ErrorOccurredAsync += OnErrorOccurredAsync;
        _ctx.ConversationEngine.InputAudioTranscriptionCompletedAsync += InputAudioTranscriptionCompletedAsync;
        _ctx.ConversationEngine.OutputAudioTranscriptionCompletedyAsync += OutputAudioTranscriptionCompletedAsync;
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

                        await _ctx.ConversationEngine.EndSessionAsync("Disconnect From RealtimeAi");
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

                        if (!_ctx.IsAiSpeaking && _ctx.Callbacks.OnAudioDataAsync != null)
                            await _ctx.Callbacks.OnAudioDataAsync(audioBytes, false).ConfigureAwait(false);

                        if (!_ctx.IsAiSpeaking)
                            await WriteToAudioBufferAsync(audioBytes).ConfigureAwait(false);

                        await _ctx.ConversationEngine.SendAudioChunkAsync(new RealtimeAiWssAudioData
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
                    await _ctx.ConversationEngine.EndSessionAsync("Client disconnected abnormally");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[RealtimeAi] Failed to end conversation engine session during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}",
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
                if (_ctx.Callbacks.OnSessionEndedAsync != null)
                    await _ctx.Callbacks.OnSessionEndedAsync(_ctx.SessionId).ConfigureAwait(false);
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

    private async Task OnSessionStatusChangedAsync(RealtimeAiWssEventType eventType, object data)
    {
        if (eventType == RealtimeAiWssEventType.SessionInitialized && _ctx.Callbacks.OnSessionReadyAsync != null)
        {
            Log.Information("[RealtimeAi] Session ready, invoking OnSessionReadyAsync, SessionId: {SessionId}", _ctx.SessionId);
            await _ctx.Callbacks.OnSessionReadyAsync(_ctx.ConversationEngine.SendTextAsync).ConfigureAwait(false);
        }
    }

    private async Task OnAiAudioOutputReadyAsync(RealtimeAiWssAudioData aiAudioData)
    {
        if (aiAudioData == null || string.IsNullOrEmpty(aiAudioData.Base64Payload)) return;

        Log.Debug("[RealtimeAi] Sending AI audio to client, SessionId: {SessionId}, AssistantId: {AssistantId}, PayloadLength: {PayloadLength}",
            _ctx.SessionId, _ctx.Options.ConnectionProfile.ProfileId, aiAudioData.Base64Payload.Length);

        _ctx.IsAiSpeaking = true;

        var audioBytes = Convert.FromBase64String(aiAudioData.Base64Payload);

        if (_ctx.Callbacks.OnAudioDataAsync != null)
            await _ctx.Callbacks.OnAudioDataAsync(audioBytes, true).ConfigureAwait(false);

        await WriteToAudioBufferAsync(audioBytes).ConfigureAwait(false);

        var audioDelta = new
        {
            type = "ResponseAudioDelta",
            Data = new
            {
                aiAudioData.Base64Payload
            },
            session_id = _ctx.StreamSid
        };

        await SendToClientAsync(audioDelta).ConfigureAwait(false);
    }

    private async Task OnAiDetectedUserSpeechAsync()
    {
        if (_ctx.Callbacks.IdleFollowUp != null)
            StopInactivityTimer();

        var speechDetected = new
        {
            type = "SpeechDetected",
            session_id = _ctx.StreamSid
        };

        await SendToClientAsync(speechDetected).ConfigureAwait(false);
    }

    private async Task OnErrorOccurredAsync(RealtimeAiErrorData errorData)
    {
        Log.Error("[RealtimeAi] Conversation engine error, SessionId: {SessionId}, AssistantId: {AssistantId}, ErrorCode: {ErrorCode}, ErrorMessage: {ErrorMessage}, IsCritical: {IsCritical}",
            _ctx.SessionId, _ctx.Options?.ConnectionProfile?.ProfileId, errorData?.Code, errorData?.Message, errorData?.IsCritical);

        var clientError = new
        {
            type = "ClientError",
            session_id = _ctx.StreamSid
        };

        await SendToClientAsync(clientError).ConfigureAwait(false);
    }

    private async Task OnAiTurnCompletedAsync(object data)
    {
        _ctx.Round += 1;
        _ctx.IsAiSpeaking = false;

        var turnCompleted = new
        {
            type = "AiTurnCompleted",
            session_id = _ctx.StreamSid
        };

        var idleFollowUp = _ctx.Callbacks.IdleFollowUp;
        if (idleFollowUp != null && (!idleFollowUp.SkipRounds.HasValue || idleFollowUp.SkipRounds.Value < _ctx.Round))
            StartInactivityTimer(idleFollowUp.TimeoutSeconds, idleFollowUp.FollowUpMessage);

        await SendToClientAsync(turnCompleted).ConfigureAwait(false);
        Log.Information("[RealtimeAi] AI turn completed, SessionId: {SessionId}, AssistantId: {AssistantId}, Round: {Round}, Data: {@Data}",
            _ctx.SessionId, _ctx.Options.ConnectionProfile.ProfileId, _ctx.Round, data);
    }

    private async Task InputAudioTranscriptionCompletedAsync(RealtimeAiWssTranscriptionData transcriptionData)
    {
        _ctx.Transcriptions.Enqueue((transcriptionData.Speaker, transcriptionData.Transcript));

        var transcription = new
        {
            type = "InputAudioTranscriptionCompleted",
            Data = new
            {
                transcriptionData
            },
            session_id = _ctx.StreamSid
        };

        await SendToClientAsync(transcription).ConfigureAwait(false);
    }

    private async Task OutputAudioTranscriptionCompletedAsync(RealtimeAiWssTranscriptionData transcriptionData)
    {
        _ctx.Transcriptions.Enqueue((transcriptionData.Speaker, transcriptionData.Transcript));

        var transcription = new
        {
            type = "OutputAudioTranscriptionCompleted",
            Data = new
            {
                transcriptionData
            },
            session_id = _ctx.StreamSid
        };

        await SendToClientAsync(transcription).ConfigureAwait(false);
    }

    private async Task HandleTranscriptionsAsync()
    {
        if (_ctx.Callbacks.OnTranscriptionsReadyAsync == null || _ctx.Transcriptions.IsEmpty) return;

        var transcriptions = _ctx.Transcriptions.Select(t => (t.Speaker, t.Text)).ToList();
        await _ctx.Callbacks.OnTranscriptionsReadyAsync(_ctx.SessionId, transcriptions).ConfigureAwait(false);
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
        if (!_ctx.Options.EnableRecording || _ctx.Callbacks.OnRecordingCompleteAsync == null) return;

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

            await _ctx.Callbacks.OnRecordingCompleteAsync(_ctx.SessionId, wavStream.ToArray()).ConfigureAwait(false);
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

            await _ctx.ConversationEngine.SendTextAsync(followUpMessage);
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

    private async Task DisposeConversationEngineAsync()
    {
        if (_ctx.ConversationEngine == null) return;

        _ctx.ConversationEngine.SessionStatusChangedAsync -= OnSessionStatusChangedAsync;
        _ctx.ConversationEngine.AiAudioOutputReadyAsync -= OnAiAudioOutputReadyAsync;
        _ctx.ConversationEngine.AiDetectedUserSpeechAsync -= OnAiDetectedUserSpeechAsync;
        _ctx.ConversationEngine.AiTurnCompletedAsync -= OnAiTurnCompletedAsync;
        _ctx.ConversationEngine.ErrorOccurredAsync -= OnErrorOccurredAsync;
        _ctx.ConversationEngine.InputAudioTranscriptionCompletedAsync -= InputAudioTranscriptionCompletedAsync;
        _ctx.ConversationEngine.OutputAudioTranscriptionCompletedyAsync -= OutputAudioTranscriptionCompletedAsync;

        await _ctx.ConversationEngine.DisposeAsync().ConfigureAwait(false);
        _ctx.ConversationEngine = null;
    }
}
