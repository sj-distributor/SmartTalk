using System.Buffers;
using Serilog;
using System.Text;
using Newtonsoft.Json;
using System.Text.Json;
using SmartTalk.Core.Ioc;
using System.Net.WebSockets;
using NAudio.Wave;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Core.Services.RealtimeAi.Wss;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAi.Adapters;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Commands.RealtimeAi;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Enums.Hr;
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
    private readonly ISmartiesClient _smartiesClient;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IAttachmentService _attachmentService;
    private readonly IRealtimeAiSwitcher _realtimeAiSwitcher;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    private string _streamSid;
    private string _imageMessage;
    private bool _cameraEnabled = false;
    private WebSocket _webSocket;
    private IRealtimeAiConversationEngine _conversationEngine;
    private Domain.AISpeechAssistant.AiSpeechAssistant _speechAssistant;

    private int _round;
    private string _sessionId;
    private volatile bool _isAiSpeaking;
    private bool _hasHandledAudioBuffer;
    private MemoryStream _wholeAudioBuffer;
    private readonly IInactivityTimerManager _inactivityTimerManager;
    private List<(AiSpeechAssistantSpeaker, string)> _conversationTranscription;

    public RealtimeAiService(
        IPhoneOrderService phoneOrderService,
        IAgentDataProvider agentDataProvider,
        IAttachmentService attachmentService,
        IRealtimeAiSwitcher realtimeAiSwitcher,
        IInactivityTimerManager inactivityTimerManager,
        IRealtimeAiConversationEngine conversationEngine,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider, ISmartiesClient smartiesClient)
    {
        _phoneOrderService = phoneOrderService;
        _agentDataProvider = agentDataProvider;
        _attachmentService = attachmentService;
        _realtimeAiSwitcher = realtimeAiSwitcher;
        _conversationEngine = conversationEngine;
        _backgroundJobClient = backgroundJobClient;
        _inactivityTimerManager = inactivityTimerManager;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _smartiesClient = smartiesClient;

        _round = 0;
        _webSocket = null;
        _isAiSpeaking = false;
        _speechAssistant = null;
        _hasHandledAudioBuffer = false;
        _conversationTranscription = [];
        _sessionId = Guid.NewGuid().ToString();
    }

    public async Task RealtimeAiConnectAsync(RealtimeAiConnectCommand command, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantWithKnowledgeAsync(command.AssistantId, cancellationToken).ConfigureAwait(false);
        var timer = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantTimerByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);

        Log.Information("Get realtime ai assistant: {@Assistant}", assistant);
        
        _speechAssistant = assistant ?? throw new Exception($"Could not find a assistant by id: {command.AssistantId}");
        _speechAssistant.Timer = timer;
        
        Log.Information("Get assistant and knowledge: {@Assistant}", assistant);
        
        if (assistant == null) throw new Exception($"Could not find a assistant by id: {command.AssistantId}");
        
        var finalPrompt = await BuildingAiSpeechAssistantKnowledgeBaseAsync(assistant, cancellationToken).ConfigureAwait(false);
        
        await RealtimeAiConnectInternalAsync(command.WebSocket, 
            !string.IsNullOrWhiteSpace(finalPrompt) ? finalPrompt : "You are a friendly assistant",
            command.InputFormat, command.OutputFormat, command.Region, command.OrderRecordType, cancellationToken).ConfigureAwait(false);
    }

    private async Task RealtimeAiConnectInternalAsync(
        WebSocket webSocket, string initialPrompt, RealtimeAiAudioCodec inputFormat,
        RealtimeAiAudioCodec outputFormat, RealtimeAiServerRegion region, PhoneOrderRecordType orderRecordType, CancellationToken cancellationToken)
    {
        _webSocket = webSocket;
        _streamSid = Guid.NewGuid().ToString("N");
        
        _conversationEngine.SessionStatusChangedAsync += OnAiSessionStatusChangedAsync;

        _isAiSpeaking = false; 
        _wholeAudioBuffer = new MemoryStream();
        
        BuildConversationEngine(_speechAssistant.ModelProvider);
        
        await _conversationEngine.StartSessionAsync(_speechAssistant, initialPrompt, inputFormat, outputFormat, region, cancellationToken).ConfigureAwait(false);
        
        await ReceiveFromWebSocketClientAsync(
            new RealtimeAiEngineContext { AssistantId = _speechAssistant.Id, InitialPrompt = initialPrompt, InputFormat = inputFormat, OutputFormat = outputFormat }, orderRecordType, cancellationToken).ConfigureAwait(false);
    }

    private void BuildConversationEngine(AiSpeechAssistantProvider provider)
    {
        var client = _realtimeAiSwitcher.WssClient(provider);
        var adapter = _realtimeAiSwitcher.ProviderAdapter(provider);
        
        _conversationEngine = new RealtimeAiConversationEngine(adapter, client);
        _conversationEngine.AiAudioOutputReadyAsync += OnAiAudioOutputReadyAsync;
        _conversationEngine.AiDetectedUserSpeechAsync += OnAiDetectedUserSpeechAsync;
        _conversationEngine.AiTurnCompletedAsync += OnAiTurnCompletedAsync;
        _conversationEngine.ErrorOccurredAsync += OnErrorOccurredAsync;
        _conversationEngine.InputAudioTranscriptionCompletedAsync += InputAudioTranscriptionCompletedAsync;
        _conversationEngine.OutputAudioTranscriptionCompletedyAsync += OutputAudioTranscriptionCompletedAsync;
        _conversationEngine.OutputAudioTranscriptionPartialAsync += OutputAudioTranscriptionPartialAsync;
    }
    
    private async Task<string> BuildingAiSpeechAssistantKnowledgeBaseAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken)
    {
        if (assistant?.Knowledge == null || string.IsNullOrEmpty(assistant.Knowledge?.Prompt)) return string.Empty;

        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");
        
        var finalPrompt = assistant.Knowledge.Prompt
            .Replace("#{current_time}", currentTime)
            .Replace("#{pst_date}", $"{pstTime.Date:yyyy-MM-dd} {pstTime.DayOfWeek}");

        if (finalPrompt.Contains("#{restaurant_info}") || finalPrompt.Contains("#{restaurant_items}"))
        {
            var aiKid = await _aiSpeechAssistantDataProvider.GetAiKidAsync(assistant.Id, cancellationToken).ConfigureAwait(false);

            if (aiKid != null)
            {
                try
                {
                    var response = await _smartiesClient.GetCrmCustomerInfoAsync(aiKid.KidUuid, cancellationToken).ConfigureAwait(false);
                     
                    Log.Information("Get crm customer info response: {@Response}", response);

                    var result = SplicingCrmCustomerResponse(response?.Data?.FirstOrDefault());

                    finalPrompt = finalPrompt.Replace("#{restaurant_info}", result.RestaurantInfo).Replace("#{restaurant_items}", result.PurchasedItems);
                }
                catch (Exception e)
                {
                    Log.Warning("Replace restaurant info failed: {Exception}", e);
                }
            }
        }
        
        if (finalPrompt.Contains("#{hr_interview_section1}", StringComparison.OrdinalIgnoreCase))
        {
            var cacheKeys = Enum.GetValues(typeof(HrInterviewQuestionSection))
                .Cast<HrInterviewQuestionSection>()
                .Select(section => "hr_interview_" + section.ToString().ToLower())
                .ToList();

            var caches = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeVariableCachesAsync(cacheKeys, cancellationToken: cancellationToken).ConfigureAwait(false);

            foreach (var section in Enum.GetValues(typeof(HrInterviewQuestionSection)).Cast<HrInterviewQuestionSection>())
            {
                var cacheKey = $"hr_interview_{section.ToString().ToLower()}";
                var placeholder = $"#{{{cacheKey}}}";

                finalPrompt = finalPrompt.Replace(placeholder, caches.FirstOrDefault(x => x.CacheKey == cacheKey)?.CacheValue);
            }
        }

        if (finalPrompt.Contains("#{hr_interview_questions}", StringComparison.OrdinalIgnoreCase))
        {
            var cache = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeVariableCachesAsync(["hr_interview_questions"], cancellationToken: cancellationToken).ConfigureAwait(false);
            
            finalPrompt = finalPrompt.Replace("#{hr_interview_questions}", cache.FirstOrDefault()?.CacheValue);   
        }
        
        Log.Information($"The final prompt: {finalPrompt}");

        return finalPrompt;
    }
    
    private (string RestaurantInfo, string PurchasedItems) SplicingCrmCustomerResponse(CrmCustomerInfoDto customerInfo)
    {
        var infoSb = new StringBuilder();
        var itemsSb = new StringBuilder();
        
        infoSb.AppendLine($"餐厅名字：{customerInfo.Name}");
        infoSb.AppendLine($"餐厅地址：{customerInfo.Address}");

        itemsSb.AppendLine("餐厅购买过的items（餐厅所需要的）：");
        
        var idx = 1;
        foreach (var product in customerInfo.Products.OrderByDescending(x => x.CreatedAt))
        {
            var itemName = product.Name;
            var specSb = new StringBuilder();
            foreach (var attr in product.Attributes)
            {
                var attrName = attr.Name;
                var options = attr.Options;
                var optionNames = string.Join("、", options.Select(opt => opt.Name.ToString()));
                specSb.Append($"{attrName}: {optionNames}; ");
            }

            if (idx < 4)
                itemsSb.AppendLine($"{idx}. {itemName}(新品)，规格: {specSb.ToString().Trim()}");
            else
                itemsSb.AppendLine($"{idx}. {itemName}，规格: {specSb.ToString().Trim()}");
            
            idx++;
        }
        
        return (infoSb.ToString(), itemsSb.ToString());
    }
    
    private async Task ReceiveFromWebSocketClientAsync(RealtimeAiEngineContext context, PhoneOrderRecordType orderRecordType, CancellationToken cancellationToken)
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
                        await HandleWholeAudioBufferAsync(orderRecordType).ConfigureAwait(false);
                        Log.Information("The Conversation transcription: {@Conversations}", _conversationTranscription);
                    
                        await _conversationEngine.EndSessionAsync("Disconnect From RealtimeAi");
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client acknowledges close", CancellationToken.None);
                        return;
                    }
                    
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
        
                ms.Seek(0, SeekOrigin.Begin);
                var rawMessage = Encoding.UTF8.GetString(ms.ToArray());
        
                Log.Information("ReceiveFromRealtimeClientAsync raw message: {@Message}", rawMessage);
        
                try
                {
                    using var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(rawMessage);
                    
                    if (jsonDocument.RootElement.TryGetProperty("start_camera", out var cameraProp))
                    {
                        _cameraEnabled = cameraProp.GetBoolean();
                        if (!_cameraEnabled)
                            _imageMessage = null;
                        Log.Information("Camera {Status}", _cameraEnabled ? "started" : "stopped");
                        continue;
                    }
                    
                    if (jsonDocument.RootElement.TryGetProperty("commit_audio", out var commit))
                    {
                        await _conversationEngine.CommitAudioAsync().ConfigureAwait(false);
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

        Log.Information("Realtime output: {@Output} 准备发送。", aiAudioData);
        
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

    private Task OnAiSessionStatusChangedAsync(RealtimeAiWssEventType type, object data)
    {
        switch (type)
        {
            case RealtimeAiWssEventType.SessionInitialized:
                Log.Information(
                    "TwilioHandler: AI 会话已成功初始化，可以开始双向通信。"); // TwilioHandler: AI session successfully initialized, bidirectional communication can begin.
                break;
            case RealtimeAiWssEventType.SessionUpdateFailed:
                Log.Error("TwilioHandler: AI 会话初始化或更新失败: {@EventData}", data); // TwilioHandler: AI session initialization or update failed: {@EventData}
                break;
        }

        return Task.CompletedTask;
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

        await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(speechDetected))), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    private async Task OnErrorOccurredAsync(RealtimeAiErrorData errorData)
    {
        await HandleWholeAudioBufferAsync(orderRecordType: PhoneOrderRecordType.TestLink).ConfigureAwait(false);
        
        var clientError = new
        {
            type = "ClientError",
            session_id = _streamSid
        };

        await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(clientError))), WebSocketMessageType.Text, true, CancellationToken.None);
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

        await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(turnCompleted))), WebSocketMessageType.Text, true, CancellationToken.None);
        Log.Information("Realtime turn completed, {@data}", data);
    }

    private async Task InputAudioTranscriptionCompletedAsync(RealtimeAiWssTranscriptionData transcriptionData)
    {
        _conversationTranscription.Add((transcriptionData.Speaker, transcriptionData.Transcript));
        
        var transcription = new
        {
            type = "InputAudioTranscriptionCompleted",
            Data = new
            { 
                transcriptionData
            },
            session_id = _streamSid
        };

        await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(transcription))), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    private async Task OutputAudioTranscriptionCompletedAsync(RealtimeAiWssTranscriptionData transcriptionData)
    {
        _conversationTranscription.Add((transcriptionData.Speaker, transcriptionData.Transcript));
        
        var transcription = new
        {
            type = "OutputAudioTranscriptionCompleted",
            Data = new
            { 
                transcriptionData
            },
            session_id = _streamSid
        };

        await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(transcription))), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    private async Task OutputAudioTranscriptionPartialAsync(RealtimeAiWssTranscriptionData transcriptionData)
    {
        var transcription = new
        {
            type = "OutputAudioTranscriptionPartial",
            Data = new
            { 
                transcriptionData
            },
            session_id = _streamSid
        };
        
        await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(transcription))), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    private async Task HandleWholeAudioBufferAsync(PhoneOrderRecordType orderRecordType)
    {
        if (_wholeAudioBuffer is { CanRead: true } src && !_hasHandledAudioBuffer)
        {
            _hasHandledAudioBuffer = true;

            var waveFormat = new WaveFormat(24000, 16, 1);
            using var wavStream = new MemoryStream();

            await using (var writer = new WaveFileWriter(wavStream, waveFormat))
            {
                var rented = ArrayPool<byte>.Shared.Rent(64 * 1024);
                try
                {
                    int read;
                    if (src.CanSeek) src.Position = 0;
                    while ((read = await src.ReadAsync(rented.AsMemory(0, rented.Length))) > 0)
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

            Log.Information("audio uploaded, url: {Url}", audio?.Attachment?.FileUrl);
            if (!string.IsNullOrEmpty(audio?.Attachment?.FileUrl) && _speechAssistant.Id != 0)
            {
                var agent = await _agentDataProvider.GetAgentByAssistantIdAsync(_speechAssistant.Id).ConfigureAwait(false);
                if (agent is { IsSendAudioRecordWechat: true })
                    await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, agent.WechatRobotKey, $"您有一条新的AI通话录音：\n{audio?.Attachment?.FileUrl}", Array.Empty<string>(), CancellationToken.None).ConfigureAwait(false);
                
                _backgroundJobClient.Enqueue<IRealtimeProcessJobService>(x =>
                    x.RecordingRealtimeAiAsync(audio.Attachment.FileUrl, _speechAssistant.Id, _sessionId, orderRecordType, CancellationToken.None));
            }

            await src.DisposeAsync();
            _wholeAudioBuffer = null;
        }
        
        await HandleTranscriptionsAsync().ConfigureAwait(false);
    }

    private async Task HandleTranscriptionsAsync()
    {
        Log.Information("Get the realtime transcriptions: {@Transcriptions}", _conversationTranscription);
        
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
    
    private void StartInactivityTimer(int seconds, string alterContent)
    {
        _inactivityTimerManager.StartTimer(_streamSid, TimeSpan.FromSeconds(seconds), async () =>
        {
            Log.Warning("No activity detected for {seconds} seconds.", seconds);

            await _conversationEngine.SendTextAsync(alterContent);
        });
    }

    private void StopInactivityTimer()
    {
        _inactivityTimerManager.StopTimer(_streamSid);
    }
}