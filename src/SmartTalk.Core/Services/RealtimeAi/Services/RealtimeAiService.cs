using System.Buffers;
using System.Collections.Concurrent;
using Serilog;
using System.Text;
using Newtonsoft.Json;
using System.Text.Json;
using SmartTalk.Core.Ioc;
using System.Net.WebSockets;
using NAudio.Wave;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Core.Services.RealtimeAi.Wss;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAi.Adapters;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Commands.RealtimeAi;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Enums.PhoneOrder;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.RealtimeAi.Services;

public interface IRealtimeAiService : IScopedDependency
{
    Task RealtimeAiConnectAsync(RealtimeAiConnectCommand command, CancellationToken cancellationToken);
}

public class RealtimeAiService : IRealtimeAiService
{
    private readonly IAttachmentService _attachmentService;
    private readonly IRealtimeAiSwitcher _realtimeAiSwitcher;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    private string _streamSid;
    private WebSocket _webSocket;
    private IRealtimeAiConversationEngine _conversationEngine;
    private Domain.AISpeechAssistant.AiSpeechAssistant _speechAssistant;

    private int _round;
    private string _sessionId;
    private volatile bool _isAiSpeaking;
    private int _hasHandledAudioBuffer;
    private MemoryStream _wholeAudioBuffer;
    private readonly IInactivityTimerManager _inactivityTimerManager;
    private readonly SemaphoreSlim _wsSendLock = new(1, 1);
    private readonly SemaphoreSlim _bufferLock = new(1, 1);
    private ConcurrentQueue<(AiSpeechAssistantSpeaker, string)> _conversationTranscription;

    public RealtimeAiService(
        IAttachmentService attachmentService,
        IRealtimeAiSwitcher realtimeAiSwitcher,
        IInactivityTimerManager inactivityTimerManager,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _attachmentService = attachmentService;
        _realtimeAiSwitcher = realtimeAiSwitcher;
        _backgroundJobClient = backgroundJobClient;
        _inactivityTimerManager = inactivityTimerManager;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;

        _round = 0;
        _webSocket = null;
        _isAiSpeaking = false;
        _speechAssistant = null;
        _hasHandledAudioBuffer = 0;
        _conversationTranscription = new ConcurrentQueue<(AiSpeechAssistantSpeaker, string)>();
        _sessionId = Guid.NewGuid().ToString();
    }

    public async Task RealtimeAiConnectAsync(RealtimeAiConnectCommand command, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantWithKnowledgeAsync(command.AssistantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not find assistant by id: {command.AssistantId}");

        var timer = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantTimerByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);

        _speechAssistant = assistant;
        _speechAssistant.Timer = timer;

        Log.Information("[RealtimeAi] Session initialized, SessionId: {SessionId}, AssistantId: {AssistantId}, Provider: {Provider}, InputFormat: {InputFormat}, OutputFormat: {OutputFormat}, Region: {Region}",
            _sessionId, _speechAssistant.Id, _speechAssistant.ModelProvider, command.InputFormat, command.OutputFormat, command.Region);

        await RealtimeAiConnectInternalAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private async Task RealtimeAiConnectInternalAsync(RealtimeAiConnectCommand command, CancellationToken cancellationToken)
    {
        _webSocket = command.WebSocket;
        _streamSid = Guid.NewGuid().ToString("N");

        _isAiSpeaking = false;
        _wholeAudioBuffer = new MemoryStream();

        var initialPrompt = "You are a friendly assistant";

        BuildConversationEngine(_speechAssistant.ModelProvider);

        try
        {
            await _conversationEngine.StartSessionAsync(_speechAssistant, initialPrompt, command.InputFormat, command.OutputFormat, command.Region, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisposeConversationEngineAsync().ConfigureAwait(false);
            await _wholeAudioBuffer.DisposeAsync().ConfigureAwait(false);
            _wholeAudioBuffer = null;
            throw;
        }

        await ReceiveFromWebSocketClientAsync(
            new RealtimeAiEngineContext { AssistantId = _speechAssistant.Id, InitialPrompt = initialPrompt, InputFormat = command.InputFormat, OutputFormat = command.OutputFormat },
            command.OrderRecordType, cancellationToken).ConfigureAwait(false);
    }

    private void BuildConversationEngine(AiSpeechAssistantProvider provider)
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

        var client = _realtimeAiSwitcher.WssClient(provider);
        var adapter = _realtimeAiSwitcher.ProviderAdapter(provider);

        _conversationEngine = new RealtimeAiConversationEngine(adapter, client);
        _conversationEngine.AiAudioOutputReadyAsync += OnAiAudioOutputReadyAsync;
        _conversationEngine.AiDetectedUserSpeechAsync += OnAiDetectedUserSpeechAsync;
        _conversationEngine.AiTurnCompletedAsync += OnAiTurnCompletedAsync;
        _conversationEngine.ErrorOccurredAsync += OnErrorOccurredAsync;
        _conversationEngine.InputAudioTranscriptionCompletedAsync += InputAudioTranscriptionCompletedAsync;
        _conversationEngine.OutputAudioTranscriptionCompletedyAsync += OutputAudioTranscriptionCompletedAsync;
    }
    
    private async Task ReceiveFromWebSocketClientAsync(RealtimeAiEngineContext context, PhoneOrderRecordType orderRecordType, CancellationToken cancellationToken)
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
                            _sessionId, _speechAssistant.Id);

                        await _conversationEngine.EndSessionAsync("Disconnect From RealtimeAi");
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client acknowledges close", CancellationToken.None);

                        Log.Information("[RealtimeAi] Session closed gracefully, SessionId: {SessionId}, AssistantId: {AssistantId}",
                            _sessionId, _speechAssistant.Id);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var rawMessage = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);

                Log.Debug("[RealtimeAi] Received client message, SessionId: {SessionId}, AssistantId: {AssistantId}, Message: {Message}",
                    _sessionId, _speechAssistant.Id, rawMessage);

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
                                { nameof(context.InputFormat), context.InputFormat }
                            }
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        Log.Warning("[RealtimeAi] Received empty payload, SessionId: {SessionId}, AssistantId: {AssistantId}",
                            _sessionId, _speechAssistant.Id);
                    }
                }
                catch (JsonException jsonEx)
                {
                    Log.Error(jsonEx, "[RealtimeAi] Failed to parse client message, SessionId: {SessionId}, AssistantId: {AssistantId}, RawMessage: {RawMessage}",
                        _sessionId, _speechAssistant.Id, rawMessage);
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Error(ex, "[RealtimeAi] WebSocket receive error, SessionId: {SessionId}, AssistantId: {AssistantId}, WebSocketState: {WebSocketState}",
                _sessionId, _speechAssistant?.Id, GetWebSocketStateSafe());
        }
        catch (OperationCanceledException)
        {
            Log.Warning("[RealtimeAi] WebSocket receive cancelled, SessionId: {SessionId}, AssistantId: {AssistantId}",
                _sessionId, _speechAssistant?.Id);
        }
        finally
        {
            if (!gracefulClose)
            {
                Log.Warning("[RealtimeAi] Client disconnected abnormally, cleaning up, SessionId: {SessionId}, AssistantId: {AssistantId}, WebSocketState: {WebSocketState}",
                    _sessionId, _speechAssistant?.Id, GetWebSocketStateSafe());

                try
                {
                    await _conversationEngine.EndSessionAsync("Client disconnected abnormally");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[RealtimeAi] Failed to end conversation engine session during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}",
                        _sessionId, _speechAssistant?.Id);
                }

                try
                {
                    _inactivityTimerManager.StopTimer(_streamSid);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[RealtimeAi] Failed to stop inactivity timer during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}, StreamSid: {StreamSid}",
                        _sessionId, _speechAssistant?.Id, _streamSid);
                }
            }

            try
            {
                await HandleWholeAudioBufferAsync(orderRecordType).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RealtimeAi] Failed to handle audio buffer during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}",
                    _sessionId, _speechAssistant?.Id);
            }

            try
            {
                await HandleTranscriptionsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RealtimeAi] Failed to handle transcriptions during cleanup, SessionId: {SessionId}, AssistantId: {AssistantId}",
                    _sessionId, _speechAssistant?.Id);
            }

            ms.Dispose();
        }
    }

    private async Task OnAiAudioOutputReadyAsync(RealtimeAiWssAudioData aiAudioData)
    {
        if (aiAudioData == null || string.IsNullOrEmpty(aiAudioData.Base64Payload)) return;

        Log.Debug("[RealtimeAi] Sending AI audio to client, SessionId: {SessionId}, AssistantId: {AssistantId}, PayloadLength: {PayloadLength}",
            _sessionId, _speechAssistant.Id, aiAudioData.Base64Payload.Length);
        
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
        if (_speechAssistant.Timer != null)
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
            _sessionId, _speechAssistant?.Id, errorData?.Code, errorData?.Message, errorData?.IsCritical);

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
        
        if (_speechAssistant.Timer != null && (_speechAssistant.Timer.SkipRound.HasValue && _speechAssistant.Timer.SkipRound.Value < _round || !_speechAssistant.Timer.SkipRound.HasValue))
            StartInactivityTimer(_speechAssistant.Timer.TimeSpanSeconds, _speechAssistant.Timer.AlterContent);

        await SendToClientAsync(turnCompleted).ConfigureAwait(false);
        Log.Information("[RealtimeAi] AI turn completed, SessionId: {SessionId}, AssistantId: {AssistantId}, Round: {Round}, Data: {@Data}",
            _sessionId, _speechAssistant.Id, _round, data);
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

    private async Task HandleWholeAudioBufferAsync(PhoneOrderRecordType orderRecordType)
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
                _sessionId, _speechAssistant.Id, audio?.Attachment?.FileUrl);

            if (!string.IsNullOrEmpty(audio?.Attachment?.FileUrl) && _speechAssistant.Id != 0)
            {
                _backgroundJobClient.Enqueue<IRealtimeProcessJobService>(x =>
                    x.RecordingRealtimeAiAsync(audio.Attachment.FileUrl, _speechAssistant.Id, _sessionId, orderRecordType, CancellationToken.None));
            }
        }
        finally
        {
            await snapshot.DisposeAsync();
        }
    }

    private async Task HandleTranscriptionsAsync()
    {
        var kid = await _aiSpeechAssistantDataProvider.GetAiKidAsync(agentId: _speechAssistant.AgentId).ConfigureAwait(false);

        if (kid == null) return;
        
        _backgroundJobClient.Enqueue<ISmartiesClient>(x =>
            x.CallBackSmartiesAiKidConversationsAsync(new AiKidConversationCallBackRequestDto
            {
                Uuid = kid.KidUuid,
                SessionId = _sessionId,
                Transcriptions = _conversationTranscription.Select(t => new RealtimeAiTranscriptionDto
                {
                    Speaker = t.Item1,
                    Transcription = t.Item2
                }).ToList()
            }, CancellationToken.None));
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
                _sessionId, _speechAssistant?.Id, _webSocket?.State);
        }
        finally
        {
            _wsSendLock.Release();
        }
    }

    private void StartInactivityTimer(int seconds, string alterContent)
    {
        _inactivityTimerManager.StartTimer(_streamSid, TimeSpan.FromSeconds(seconds), async () =>
        {
            Log.Warning("[RealtimeAi] Inactivity timeout triggered, SessionId: {SessionId}, AssistantId: {AssistantId}, TimeoutSeconds: {TimeoutSeconds}",
                _sessionId, _speechAssistant.Id, seconds);

            await _conversationEngine.SendTextAsync(alterContent);
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