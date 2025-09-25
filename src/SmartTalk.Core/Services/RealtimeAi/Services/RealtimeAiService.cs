using Serilog;
using System.Text;
using Newtonsoft.Json;
using System.Text.Json;
using SmartTalk.Core.Ioc;
using System.Net.WebSockets;
using NAudio.Wave;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Core.Services.RealtimeAi.Wss;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAi.Adapters;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Commands.RealtimeAi;
using SmartTalk.Messages.Dto.Attachments;
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
    private string _imageMessage;
    private bool _cameraEnabled = false;
    private WebSocket _webSocket;
    private IRealtimeAiConversationEngine _conversationEngine;
    
    private volatile bool _isAiSpeaking;
    private MemoryStream _wholeAudioBuffer;

    public RealtimeAiService(
        IAttachmentService attachmentService,
        IRealtimeAiSwitcher realtimeAiSwitcher,
        IRealtimeAiConversationEngine conversationEngine,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _attachmentService = attachmentService;
        _realtimeAiSwitcher = realtimeAiSwitcher;
        _conversationEngine = conversationEngine;
        _backgroundJobClient = backgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;

        _webSocket = null;
        _isAiSpeaking = false;
    }
    
    public async Task RealtimeAiConnectAsync(RealtimeAiConnectCommand command, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantWithKnowledgeAsync(command.AssistantId, cancellationToken).ConfigureAwait(false);
        
        if (assistant == null) throw new Exception($"Could not find a assistant by id: {command.AssistantId}");
        
        await RealtimeAiConnectInternalAsync(command.WebSocket, assistant, 
            "You are a friendly assistant", command.InputFormat, command.OutputFormat, cancellationToken).ConfigureAwait(false);
    }

    private async Task RealtimeAiConnectInternalAsync(WebSocket webSocket, Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        string initialPrompt, RealtimeAiAudioCodec inputFormat, RealtimeAiAudioCodec outputFormat, CancellationToken cancellationToken)
    {
        _webSocket = webSocket;
        _streamSid = Guid.NewGuid().ToString("N");
        
        _isAiSpeaking = false; 
        _wholeAudioBuffer = new MemoryStream();
        
        BuildConversationEngine(assistant.ModelProvider);
        
        await _conversationEngine.StartSessionAsync(assistant, initialPrompt, inputFormat, outputFormat, cancellationToken).ConfigureAwait(false);
        
        await ReceiveFromWebSocketClientAsync(
            new RealtimeAiEngineContext { AgentId = assistant.AgentId, InitialPrompt = initialPrompt, InputFormat = inputFormat, OutputFormat = outputFormat }, cancellationToken).ConfigureAwait(false);
    }

    private void BuildConversationEngine(AiSpeechAssistantProvider provider)
    {
        var client = _realtimeAiSwitcher.WssClient(provider);
        var adapter = _realtimeAiSwitcher.ProviderAdapter(provider);
        
        _conversationEngine = new RealtimeAiConversationEngine(adapter, client);
        _conversationEngine.AiAudioOutputReadyAsync += OnAiAudioOutputReadyAsync;
        _conversationEngine.AiDetectedUserSpeechAsync += OnAiDetectedUserSpeechAsync;
        _conversationEngine.AiTurnCompletedAsync += OnAiTurnCompletedAsync;
    }
    
    private async Task ReceiveFromWebSocketClientAsync(RealtimeAiEngineContext context, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        try
        {
            while (_webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                using var ms = new MemoryStream();
                
                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (_wholeAudioBuffer is { Length: > 0 })
                        {
                            var waveFormat = new WaveFormat(24000, 16, 1);
                            using (var wavStream = new MemoryStream())
                            await using (var writer = new WaveFileWriter(wavStream, waveFormat))
                            {
                                writer.Write(_wholeAudioBuffer.ToArray(), 0, (int)_wholeAudioBuffer.Length);
                                writer.Flush();
                                var audio = await _attachmentService.UploadAttachmentAsync(new UploadAttachmentCommand { Attachment = new UploadAttachmentDto { FileName = Guid.NewGuid() + ".wav", FileContent = wavStream.ToArray(), } }, cancellationToken).ConfigureAwait(false);
                            
                                Log.Information("audio uploaded, url: {Url}", audio?.Attachment?.FileUrl);
                                _backgroundJobClient.Enqueue<IRealtimeProcessJobService>(x => x.RecordingRealtimeAiAsync(audio.Attachment.FileUrl, context.AgentId, cancellationToken));
                            }
                            
                            await _wholeAudioBuffer.DisposeAsync();
                            _wholeAudioBuffer = null;
                        }
                        
                        await _conversationEngine.EndSessionAsync("Disconnect From RealtimeAi");
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client acknowledges close", CancellationToken.None);
                        return;
                    }
                    
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
        
                ms.Seek(0, SeekOrigin.Begin);
                var rawMessage = Encoding.UTF8.GetString(ms.ToArray());
        
                Log.Debug("ReceiveFromRealtimeClientAsync raw message: {@Message}", rawMessage);
        
                try
                {
                    using var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(rawMessage);
                    
                    if (jsonDocument.RootElement.TryGetProperty("start_camera", out var cameraProp))
                    {
                                _cameraEnabled = false;
                        _cameraEnabled = cameraProp.GetBoolean();
                        if (!_cameraEnabled)
                            _imageMessage = null;
                        Log.Information("Camera {Status}", _cameraEnabled ? "started" : "stopped");
                        continue;
                    }

                    var payload = jsonDocument?.RootElement.GetProperty("media").GetProperty("payload").GetString();
                    
                    if (!string.IsNullOrWhiteSpace(payload))
                    {
                        if (!_isAiSpeaking && _wholeAudioBuffer != null)
                            await _wholeAudioBuffer.WriteAsync(Convert.FromBase64String(payload), cancellationToken).ConfigureAwait(false);

                        var inputFormat = context.InputFormat;
                        if (jsonDocument.RootElement.TryGetProperty("media", out var mediaElement) && mediaElement.TryGetProperty("type", out var typeElement))
                            inputFormat = typeElement.GetString() switch
                            {
                                "audio" => context.InputFormat,
                                "video" => RealtimeAiAudioCodec.IMAGE,
                                _ => context.InputFormat
                            };

                        if (inputFormat == RealtimeAiAudioCodec.IMAGE)
                        {
                            _imageMessage = payload;
                        }
                        
                        var customProps = new Dictionary<string, object>
                        {
                            { nameof(context.InputFormat), context.InputFormat }
                        };

                        if (!string.IsNullOrEmpty(_imageMessage))
                            customProps["image"] = _imageMessage;

                        await _conversationEngine.SendAudioChunkAsync(new RealtimeAiWssAudioData
                        {
                            Base64Payload = payload,
                            CustomProperties = customProps
                        }).ConfigureAwait(false);

                        _imageMessage = null;
                    }
                    else
                    {
                        Log.Warning("ReceiveFromRealtimeClientAsync: payload is null or empty.");
                    }
                }
                catch (JsonException jsonEx)
                {
                    Log.Error("Failed to parse incoming JSON: {Error}. Raw: {Raw}", jsonEx.Message, rawMessage);
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Error("Receive from realtime error: {@ex}", ex);
        }
    }
    
    private async Task OnAiAudioOutputReadyAsync(RealtimeAiWssAudioData aiAudioData)
    {
        if (aiAudioData == null || string.IsNullOrEmpty(aiAudioData.Base64Payload)) return;

        Log.Information("Realtime output 准备发送。");
        
        _isAiSpeaking = true;
        var aiAudioBytes = Convert.FromBase64String(aiAudioData.Base64Payload);
        if (_wholeAudioBuffer != null)
            await _wholeAudioBuffer.WriteAsync(aiAudioBytes, CancellationToken.None).ConfigureAwait(false);
        
        var audioDelta = new
        {
            type = "ResponseAudioDelta",
            Data = new
            { 
                aiAudioData.Base64Payload
            },
            session_id = _streamSid
        };

        await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(audioDelta))), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task OnAiDetectedUserSpeechAsync()
    {
        var speechDetected = new
        {
            type = "SpeechDetected",
            session_id = _streamSid
        };

        await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(speechDetected))), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task OnAiTurnCompletedAsync(object data)
    {
        _isAiSpeaking = false;
        
        var turnCompleted = new
        {
            type = "AiTurnCompleted",
            session_id = _streamSid
        };

        await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(turnCompleted))), WebSocketMessageType.Text, true, CancellationToken.None);
        Log.Information("Realtime turn completed, {@data}", data);
    }
}