using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NAudio.Wave;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Wss;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public interface IRealtimeAiService : IScopedDependency
{
    Task StartAsync(RealtimeSessionOptions options, RealtimeSessionCallbacks callbacks, CancellationToken cancellationToken);
}

public class RealtimeAiService : IRealtimeAiService
{
    private readonly IAttachmentService _attachmentService;
    private readonly IRealtimeAiSwitcher _realtimeAiSwitcher;
    private readonly IInactivityTimerManager _inactivityTimerManager;

    private string _streamSid;
    private WebSocket _webSocket;
    private IRealtimeAiConversationEngine _conversationEngine;
    private RealtimeSessionOptions _options;

    private int _round;
    private string _sessionId;
    private volatile bool _isAiSpeaking;
    private int _hasHandledAudioBuffer;
    private MemoryStream _wholeAudioBuffer;
    private RealtimeSessionCallbacks _callbacks;
    private readonly SemaphoreSlim _wsSendLock = new(1, 1);
    private readonly SemaphoreSlim _bufferLock = new(1, 1);
    private ConcurrentQueue<(AiSpeechAssistantSpeaker, string)> _conversationTranscription;

    public RealtimeAiService(
        IAttachmentService attachmentService,
        IRealtimeAiSwitcher realtimeAiSwitcher,
        IInactivityTimerManager inactivityTimerManager)
    {
        _attachmentService = attachmentService;
        _realtimeAiSwitcher = realtimeAiSwitcher;
        _inactivityTimerManager = inactivityTimerManager;

        _round = 0;
        _webSocket = null;
        _isAiSpeaking = false;
        _options = null;
        _hasHandledAudioBuffer = 0;
        _conversationTranscription = new ConcurrentQueue<(AiSpeechAssistantSpeaker, string)>();
        _sessionId = Guid.NewGuid().ToString();
    }

    public async Task StartAsync(RealtimeSessionOptions options, RealtimeSessionCallbacks callbacks, CancellationToken cancellationToken)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _callbacks = callbacks ?? new RealtimeSessionCallbacks();

        _options.ModelConfig = _options.ModelConfig ?? throw new ArgumentNullException(nameof(_options.ModelConfig));
        _options.ConnectionProfile = _options.ConnectionProfile ?? throw new ArgumentNullException(nameof(_options.ConnectionProfile));
        
        Log.Information("[RealtimeAi] Session initialized, SessionId: {SessionId}, ProfileId: {ProfileId}, Provider: {Provider}, InputFormat: {InputFormat}, OutputFormat: {OutputFormat}, Region: {Region}",
            _sessionId, _options.ConnectionProfile.ProfileId, _options.ModelConfig.Provider, options.InputFormat, options.OutputFormat, options.Region);

        _webSocket = options.WebSocket;
        _streamSid = Guid.NewGuid().ToString("N");

        _isAiSpeaking = false;
        _wholeAudioBuffer = new MemoryStream();

        BuildConversationEngine();

        try
        {
            await _conversationEngine.StartSessionAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisposeConversationEngineAsync().ConfigureAwait(false);
            await _wholeAudioBuffer.DisposeAsync().ConfigureAwait(false);
            _wholeAudioBuffer = null;
            throw;
        }

        await ReceiveFromWebSocketClientAsync(cancellationToken).ConfigureAwait(false);
    }

    private void BuildConversationEngine()
    {
        if (_conversationEngine != null)
        {
            _conversationEngine.AiAudioOutputReadyAsync -= OnAiAudioOutputReadyAsync;
            _conversationEngine.AiDetectedUserSpeechAsync -= OnAiDetectedUserSpeechAsync;
            _conversationEngine.AiTurnCompletedAsync -= OnAiTurnCompletedAsync;
            _conversationEngine.ErrorOccurredAsync -= OnErrorOccurredAsync;
            _conversationEngine.InputAudioTranscriptionCompletedAsync -= InputAudioTranscriptionCompletedAsync;
            _conversationEngine.OutputAudioTranscriptionCompletedyAsync -= OutputAudioTranscriptionCompletedAsync;
        }

        var client = _realtimeAiSwitcher.WssClient(_options.ModelConfig.Provider);
        var adapter = _realtimeAiSwitcher.ProviderAdapter(_options.ModelConfig.Provider);

        _conversationEngine = new RealtimeAiConversationEngine(adapter, client);
        _conversationEngine.AiAudioOutputReadyAsync += OnAiAudioOutputReadyAsync;
        _conversationEngine.AiDetectedUserSpeechAsync += OnAiDetectedUserSpeechAsync;
        _conversationEngine.AiTurnCompletedAsync += OnAiTurnCompletedAsync;
        _conversationEngine.ErrorOccurredAsync += OnErrorOccurredAsync;
        _conversationEngine.InputAudioTranscriptionCompletedAsync += InputAudioTranscriptionCompletedAsync;
        _conversationEngine.OutputAudioTranscriptionCompletedyAsync += OutputAudioTranscriptionCompletedAsync;
    }

    private async Task ReceiveFromWebSocketClientAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var gracefulClose = false;
        var ms = new MemoryStream();

        try
        {
            while (_webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                ms.SetLength(0);

                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        gracefulClose = true;

                        Log.Information("[RealtimeAi] Client sent close frame, SessionId: {SessionId}, AssistantId: {AssistantId}",
                            _sessionId, _options.ConnectionProfile.ProfileId);

                        await _conversationEngine.EndSessionAsync("Disconnect From RealtimeAi");
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client acknowledges close", CancellationToken.None);

                        Log.Information("[RealtimeAi] Session closed gracefully, SessionId: {SessionId}, AssistantId: {AssistantId}",
                            _sessionId, _options.ConnectionProfile.ProfileId);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var rawMessage = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);

                Log.Debug("[RealtimeAi] Received client message, SessionId: {SessionId}, AssistantId: {AssistantId}, Message: {Message}",
                    _sessionId, _options.ConnectionProfile.ProfileId, rawMessage);

                try
                {
                    using var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(rawMessage);
                    var payload = jsonDocument?.RootElement.GetProperty("media").GetProperty("payload").GetString();

                    if (!string.IsNullOrWhiteSpace(payload))
                    {
                        if (!_isAiSpeaking)
                            await WriteToAudioBufferAsync(Convert.FromBase64String(payload)).ConfigureAwait(false);

                        await _conversationEngine.SendAudioChunkAsync(new RealtimeAiWssAudioData
                        {
                            Base64Payload = payload,
                            CustomProperties = new Dictionary<string, object>
                            {
                                { nameof(RealtimeSessionOptions.InputFormat), _options.InputFormat }
                            }
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        Log.Warning("[RealtimeAi] Received empty payload, SessionId: {SessionId}, AssistantId: {AssistantId}",
                            _sessionId, _options.ConnectionProfile.ProfileId);
                    }
                }
                catch (JsonException jsonEx)
                {
                    Log.Error(jsonEx, "[RealtimeAi] Failed to parse client message, SessionId: {SessionId}, AssistantId: {AssistantId}, RawMessage: {RawMessage}",
                        _sessionId, _options.ConnectionProfile.ProfileId, rawMessage);
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Error(ex, "[RealtimeAi] WebSocket receive error, SessionId: {SessionId}, AssistantId: {AssistantId}, WebSocketState: {WebSocketState}",
                _sessionId, _options?.ConnectionProfile?.ProfileId, GetWebSocketStateSafe());
        }
        catch (OperationCanceledException)
        {
            Log.Warning("[RealtimeAi] WebSocket receive cancelled, SessionId: {SessionId}, AssistantId: {AssistantId}",
                _sessionId, _options?.ConnectionProfile?.ProfileId);
        }
        finally
        {
            if (!gracefulClose)
            {
                Log.Warning("[RealtimeAi] Client disconnected abnormally, cleaning up, SessionId: {SessionId}, AssistantId: {AssistantId}, WebSocketState: {WebSocketState}",
                    _sessionId, _options?.ConnectionProfile?.ProfileId, GetWebSocketStateSafe());

                try
                {
                    await _conversationEngine.EndSessionAsync("Client disconnected abnormally");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[RealtimeAi] Failed to end conversation engine session during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}",
                        _sessionId, _options?.ConnectionProfile?.ProfileId);
                }

                try
                {
                    _inactivityTimerManager.StopTimer(_streamSid);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[RealtimeAi] Failed to stop inactivity timer during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}, StreamSid: {StreamSid}",
                        _sessionId, _options?.ConnectionProfile?.ProfileId, _streamSid);
                }
            }

            try
            {
                await HandleWholeAudioBufferAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RealtimeAi] Failed to handle audio buffer during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}",
                    _sessionId, _options?.ConnectionProfile?.ProfileId);
            }

            try
            {
                await HandleTranscriptionsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RealtimeAi] Failed to handle transcriptions during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}",
                    _sessionId, _options?.ConnectionProfile?.ProfileId);
            }

            ms.Dispose();
        }
    }

    private async Task OnAiAudioOutputReadyAsync(RealtimeAiWssAudioData aiAudioData)
    {
        if (aiAudioData == null || string.IsNullOrEmpty(aiAudioData.Base64Payload)) return;

        Log.Debug("[RealtimeAi] Sending AI audio to client, SessionId: {SessionId}, AssistantId: {AssistantId}, PayloadLength: {PayloadLength}",
            _sessionId, _options.ConnectionProfile.ProfileId, aiAudioData.Base64Payload.Length);

        _isAiSpeaking = true;
        await WriteToAudioBufferAsync(Convert.FromBase64String(aiAudioData.Base64Payload)).ConfigureAwait(false);

        var audioDelta = new
        {
            type = "ResponseAudioDelta",
            Data = new
            {
                aiAudioData.Base64Payload
            },
            session_id = _streamSid
        };

        await SendToClientAsync(audioDelta).ConfigureAwait(false);
    }

    private async Task OnAiDetectedUserSpeechAsync()
    {
        if (_callbacks.IdleFollowUp != null)
            StopInactivityTimer();

        var speechDetected = new
        {
            type = "SpeechDetected",
            session_id = _streamSid
        };

        await SendToClientAsync(speechDetected).ConfigureAwait(false);
    }

    private async Task OnErrorOccurredAsync(RealtimeAiErrorData errorData)
    {
        Log.Error("[RealtimeAi] Conversation engine error, SessionId: {SessionId}, AssistantId: {AssistantId}, ErrorCode: {ErrorCode}, ErrorMessage: {ErrorMessage}, IsCritical: {IsCritical}",
            _sessionId, _options?.ConnectionProfile?.ProfileId, errorData?.Code, errorData?.Message, errorData?.IsCritical);

        var clientError = new
        {
            type = "ClientError",
            session_id = _streamSid
        };

        await SendToClientAsync(clientError).ConfigureAwait(false);
    }

    private async Task OnAiTurnCompletedAsync(object data)
    {
        _round += 1;
        _isAiSpeaking = false;

        var turnCompleted = new
        {
            type = "AiTurnCompleted",
            session_id = _streamSid
        };

        var idleFollowUp = _callbacks.IdleFollowUp;
        if (idleFollowUp != null && (!idleFollowUp.SkipRounds.HasValue || idleFollowUp.SkipRounds.Value < _round))
            StartInactivityTimer(idleFollowUp.TimeoutSeconds, idleFollowUp.FollowUpMessage);

        await SendToClientAsync(turnCompleted).ConfigureAwait(false);
        Log.Information("[RealtimeAi] AI turn completed, SessionId: {SessionId}, AssistantId: {AssistantId}, Round: {Round}, Data: {@Data}",
            _sessionId, _options.ConnectionProfile.ProfileId, _round, data);
    }

    private async Task InputAudioTranscriptionCompletedAsync(RealtimeAiWssTranscriptionData transcriptionData)
    {
        _conversationTranscription.Enqueue((transcriptionData.Speaker, transcriptionData.Transcript));

        var transcription = new
        {
            type = "InputAudioTranscriptionCompleted",
            Data = new
            {
                transcriptionData
            },
            session_id = _streamSid
        };

        await SendToClientAsync(transcription).ConfigureAwait(false);
    }

    private async Task OutputAudioTranscriptionCompletedAsync(RealtimeAiWssTranscriptionData transcriptionData)
    {
        _conversationTranscription.Enqueue((transcriptionData.Speaker, transcriptionData.Transcript));

        var transcription = new
        {
            type = "OutputAudioTranscriptionCompleted",
            Data = new
            {
                transcriptionData
            },
            session_id = _streamSid
        };

        await SendToClientAsync(transcription).ConfigureAwait(false);
    }

    private async Task WriteToAudioBufferAsync(byte[] data)
    {
        await _bufferLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_wholeAudioBuffer is { CanWrite: true })
                await _wholeAudioBuffer.WriteAsync(data).ConfigureAwait(false);
        }
        finally
        {
            _bufferLock.Release();
        }
    }

    private async Task HandleWholeAudioBufferAsync()
    {
        if (Interlocked.CompareExchange(ref _hasHandledAudioBuffer, 1, 0) != 0)
            return;

        MemoryStream snapshot;

        await _bufferLock.WaitAsync().ConfigureAwait(false);
        try
        {
            snapshot = _wholeAudioBuffer;
            _wholeAudioBuffer = null;
        }
        finally
        {
            _bufferLock.Release();
        }

        if (snapshot is not { CanRead: true }) return;

        try
        {
            var waveFormat = new WaveFormat(24000, 16, 1);
            using var wavStream = new MemoryStream();

            await using (var writer = new WaveFileWriter(wavStream, waveFormat))
            {
                var rented = ArrayPool<byte>.Shared.Rent(64 * 1024);
                try
                {
                    int read;
                    if (snapshot.CanSeek) snapshot.Position = 0;
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

            var audio = await _attachmentService.UploadAttachmentAsync(
                new UploadAttachmentCommand
                {
                    Attachment = new UploadAttachmentDto
                    {
                        FileName = Guid.NewGuid() + ".wav",
                        FileContent = wavStream.ToArray(),
                    }
                }, CancellationToken.None).ConfigureAwait(false);

            Log.Information("[RealtimeAi] Audio uploaded, SessionId: {SessionId}, AssistantId: {AssistantId}, Url: {Url}",
                _sessionId, _options?.ConnectionProfile?.ProfileId, audio?.Attachment?.FileUrl);

            if (!string.IsNullOrEmpty(audio?.Attachment?.FileUrl) && _callbacks.OnRecordingSavedAsync != null)
            {
                await _callbacks.OnRecordingSavedAsync(audio.Attachment.FileUrl, _sessionId).ConfigureAwait(false);
            }
        }
        finally
        {
            await snapshot.DisposeAsync();
        }
    }

    private async Task HandleTranscriptionsAsync()
    {
        if (_callbacks.OnTranscriptionsReadyAsync == null || _conversationTranscription.IsEmpty) return;

        var transcriptions = _conversationTranscription.Select(t => (t.Item1, t.Item2)).ToList();
        await _callbacks.OnTranscriptionsReadyAsync(_sessionId, transcriptions).ConfigureAwait(false);
    }

    private async Task SendToClientAsync(object payload)
    {
        if (_webSocket is not { State: WebSocketState.Open }) return;

        await _wsSendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_webSocket is not { State: WebSocketState.Open }) return;

            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (WebSocketException ex)
        {
            Log.Warning(ex, "[RealtimeAi] Failed to send message to client, SessionId: {SessionId}, AssistantId: {AssistantId}, WebSocketState: {WebSocketState}",
                _sessionId, _options?.ConnectionProfile?.ProfileId, _webSocket?.State);
        }
        finally
        {
            _wsSendLock.Release();
        }
    }

    private void StartInactivityTimer(int seconds, string followUpMessage)
    {
        _inactivityTimerManager.StartTimer(_streamSid, TimeSpan.FromSeconds(seconds), async () =>
        {
            Log.Warning("[RealtimeAi] Idle follow-up triggered, SessionId: {SessionId}, AssistantId: {AssistantId}, TimeoutSeconds: {TimeoutSeconds}",
                _sessionId, _options.ConnectionProfile.ProfileId, seconds);

            await _conversationEngine.SendTextAsync(followUpMessage);
        });
    }

    private void StopInactivityTimer()
    {
        _inactivityTimerManager.StopTimer(_streamSid);
    }

    private string GetWebSocketStateSafe()
    {
        try { return _webSocket?.State.ToString() ?? "null"; }
        catch { return "unknown"; }
    }

    private async Task DisposeConversationEngineAsync()
    {
        if (_conversationEngine == null) return;

        _conversationEngine.AiAudioOutputReadyAsync -= OnAiAudioOutputReadyAsync;
        _conversationEngine.AiDetectedUserSpeechAsync -= OnAiDetectedUserSpeechAsync;
        _conversationEngine.AiTurnCompletedAsync -= OnAiTurnCompletedAsync;
        _conversationEngine.ErrorOccurredAsync -= OnErrorOccurredAsync;
        _conversationEngine.InputAudioTranscriptionCompletedAsync -= InputAudioTranscriptionCompletedAsync;
        _conversationEngine.OutputAudioTranscriptionCompletedyAsync -= OutputAudioTranscriptionCompletedAsync;

        await _conversationEngine.DisposeAsync().ConfigureAwait(false);
        _conversationEngine = null;
    }
}