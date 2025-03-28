using Twilio;
using Serilog;
using System.Text;
using Twilio.TwiML;
using Mediator.Net;
using Newtonsoft.Json;
using System.Text.Json;
using Twilio.TwiML.Voice;
using SmartTalk.Core.Ioc;
using Twilio.AspNet.Core;
using System.Net.WebSockets;
using AutoMapper;
using SmartTalk.Core.Constants;
using Microsoft.AspNetCore.Http;
using NAudio.Codecs;
using NAudio.Wave;
using OpenAI.Chat;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Messages.Constants;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Messages.Dto.OpenAi;
using Twilio.Rest.Api.V2010.Account;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Core.Settings.ZhiPuAi;
using Task = System.Threading.Tasks.Task;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Events.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.PhoneOrder;
using JsonSerializer = System.Text.Json.JsonSerializer;
using RecordingResource = Twilio.Rest.Api.V2010.Account.Call.RecordingResource;
using Stream = Twilio.TwiML.Voice.Stream;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService : IScopedDependency
{
    CallAiSpeechAssistantResponse CallAiSpeechAssistant(CallAiSpeechAssistantCommand command);

    Task<AiSpeechAssistantConnectCloseEvent> ConnectAiSpeechAssistantAsync(ConnectAiSpeechAssistantCommand command, CancellationToken cancellationToken);

    Task RecordAiSpeechAssistantCallAsync(RecordAiSpeechAssistantCallCommand command, CancellationToken cancellationToken);

    Task ReceivePhoneRecordingStatusCallbackAsync(ReceivePhoneRecordingStatusCallbackCommand command, CancellationToken cancellationToken);
    
    Task TransferHumanServiceAsync(TransferHumanServiceCommand command, CancellationToken cancellationToken);

    Task HangupCallAsync(string callSid, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService : IAiSpeechAssistantService
{
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IFfmpegService _ffmpegService;
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly ZhiPuAiSettings _zhiPuAiSettings;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    
    private readonly ClientWebSocket _openaiClientWebSocket;
    private AiSpeechAssistantStreamContextDto _aiSpeechAssistantStreamContext;

    private bool _shouldSendBuffToOpenAi;
    private readonly List<byte[]> _wholeAudioBufferBytes;

    public AiSpeechAssistantService(
        IMapper mapper,
        ICurrentUser currentUser,
        IFfmpegService ffmpegService,
        OpenAiSettings openAiSettings,
        TwilioSettings twilioSettings,
        ISmartiesClient smartiesClient,
        ZhiPuAiSettings zhiPuAiSettings,
        IPhoneOrderService phoneOrderService,
        IAgentDataProvider agentDataProvider,
        ISmartTalkHttpClientFactory httpClientFactory,
        IRestaurantDataProvider restaurantDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _mapper = mapper;
        _currentUser = currentUser;
        _ffmpegService = ffmpegService;
        _openAiSettings = openAiSettings;
        _twilioSettings = twilioSettings;
        _smartiesClient = smartiesClient;
        _zhiPuAiSettings = zhiPuAiSettings;
        _agentDataProvider = agentDataProvider;
        _phoneOrderService = phoneOrderService;
        _httpClientFactory = httpClientFactory;
        _backgroundJobClient = backgroundJobClient;
        _restaurantDataProvider = restaurantDataProvider;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;

        _openaiClientWebSocket = new ClientWebSocket();
        _aiSpeechAssistantStreamContext = new AiSpeechAssistantStreamContextDto();

        _shouldSendBuffToOpenAi = true;
        _wholeAudioBufferBytes = [];
    }

    public CallAiSpeechAssistantResponse CallAiSpeechAssistant(CallAiSpeechAssistantCommand command)
    {
        var response = new VoiceResponse();
        var connect = new Connect();

        connect.Stream(url: $"wss://{command.Host}/api/AiSpeechAssistant/connect/{command.From}/{command.To}");
        
        response.Append(connect);

        var twiMlResult = Results.Extensions.TwiML(response);

        return new CallAiSpeechAssistantResponse { Data = twiMlResult };
    }

    public async Task<AiSpeechAssistantConnectCloseEvent> ConnectAiSpeechAssistantAsync(ConnectAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        Log.Information($"The call from {command.From} to {command.To} is connected");

        var (assistant, knowledge, prompt) = await BuildingAiSpeechAssistantKnowledgeBaseAsync(command.From, command.To, command.AssistantId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(prompt)) return new AiSpeechAssistantConnectCloseEvent();

        var humanContact = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantHumanContactByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);
        
        await ConnectOpenAiRealTimeSocketAsync(assistant, prompt, cancellationToken).ConfigureAwait(false);
        
        _aiSpeechAssistantStreamContext = new AiSpeechAssistantStreamContextDto
        {
            Host = command.Host,
            LastPrompt = prompt,
            HumanContactPhone = humanContact?.HumanPhone,
            LastUserInfo = new AiSpeechAssistantUserInfoDto
            {
                PhoneNumber = command.From
            },
            Assistant = _mapper.Map<AiSpeechAssistantDto>(assistant),
            Knowledge = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge)
        };
        
        var receiveFromTwilioTask = ReceiveFromTwilioAsync(command.TwilioWebSocket, cancellationToken);
        var sendToTwilioTask = SendToTwilioAsync(command.TwilioWebSocket, cancellationToken);

        try
        {
            await Task.WhenAll(receiveFromTwilioTask, sendToTwilioTask);
        }
        catch (Exception ex)
        {
            Log.Information("Error in one of the tasks {@Ex}", ex);
        }
        
        return new AiSpeechAssistantConnectCloseEvent();
    }

    public async Task RecordAiSpeechAssistantCallAsync(RecordAiSpeechAssistantCallCommand command, CancellationToken cancellationToken)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
        
        await RecordingResource.CreateAsync(pathCallSid: command.CallSid, recordingStatusCallbackMethod: Twilio.Http.HttpMethod.Post,
            recordingStatusCallback: new Uri($"https://{command.Host}/api/AiSpeechAssistant/recording/callback"));
    }

    public async Task ReceivePhoneRecordingStatusCallbackAsync(ReceivePhoneRecordingStatusCallbackCommand command, CancellationToken cancellationToken)
    {
        Log.Information("Handling receive phone record: {@command}", command);

        var (record, agent, aiSpeechAssistant) = await _phoneOrderDataProvider.GetRecordWithAgentAndAssistantAsync(command.CallSid, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get phone order record: {@record}", record);

        record.Url = command.RecordingUrl;
        record.Status = PhoneOrderRecordStatus.Sent;
        
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);
        var audioFileRawBytes = await _httpClientFactory.GetAsync<byte[]>(record.Url, cancellationToken).ConfigureAwait(false);
        var audioData = BinaryData.FromBytes(audioFileRawBytes);
        List<ChatMessage> messages =
        [
            new SystemChatMessage(string.IsNullOrEmpty(aiSpeechAssistant?.CustomRecordAnalyzePrompt)
                ? "你是一名電話錄音的分析員，通過聽取錄音內容和語氣情緒作出精確分析，冩出一份分析報告。\n\n分析報告的格式：交談主題：xxx\n\n 內容摘要:xxx \n\n 客人情感與情緒: xxx \n\n 待辦事件: \n1.xxx\n2.xxx \n\n 客人下單內容(如果沒有則忽略)：1. 牛肉(1箱)\n2.雞腿肉(1箱)" 
                : aiSpeechAssistant.CustomRecordAnalyzePrompt),
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage("幫我根據錄音生成分析報告：")
        ];
        
        ChatCompletionOptions options = new() { ResponseModalities = ChatResponseModalities.Text };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        Log.Information("sales record analyze report:" + completion.Content.FirstOrDefault()?.Text);
        record.TranscriptionText = completion.Content.FirstOrDefault()?.Text;

        if (agent.SourceSystem == AgentSourceSystem.Smarties)
            await _smartiesClient.CallBackSmartiesAiSpeechAssistantRecordAsync(new AiSpeechAssistantCallBackRequestDto { CallSid = command.CallSid, RecordUrl = record.Url, RecordAnalyzeReport =  record.TranscriptionText }, cancellationToken).ConfigureAwait(false);
        
        if (!string.IsNullOrEmpty(agent.WechatRobotKey))
            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(audioFileRawBytes, agent.WechatRobotKey, "錄音分析報告：\n" + record.TranscriptionText, cancellationToken).ConfigureAwait(false);

        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task TransferHumanServiceAsync(TransferHumanServiceCommand command, CancellationToken cancellationToken)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
        
        var call = await CallResource.UpdateAsync(
            pathSid: command.CallSid,
            twiml: $"<Response>\n    <Dial>\n      <Number>{command.HumanPhone}</Number>\n    </Dial>\n  </Response>"
        );
    }

    public async Task HangupCallAsync(string callSid, CancellationToken cancellationToken)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
        
        await CallResource.UpdateAsync(
            pathSid: callSid,
            status: CallResource.UpdateStatusEnum.Completed
        );
    }

    private async Task<(Domain.AISpeechAssistant.AiSpeechAssistant Assistant, AiSpeechAssistantKnowledge Knowledge, string Prompt)> BuildingAiSpeechAssistantKnowledgeBaseAsync(string from, string to, int? assistantId, CancellationToken cancellationToken)
    {
        var (assistant, knowledge, userProfile) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync(from, to, assistantId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Matching Ai speech assistant: {@Assistant}、{@Knowledge}、{@UserProfile}", assistant, knowledge, userProfile);

        if (assistant == null || knowledge == null || string.IsNullOrEmpty(knowledge.Prompt)) return (assistant, knowledge, string.Empty);

        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");
        
        var finalPrompt = knowledge.Prompt
            .Replace("#{user_profile}", string.IsNullOrEmpty(userProfile?.ProfileJson) ? " " : userProfile.ProfileJson)
            .Replace("#{current_time}", currentTime)
            .Replace("#{customer_phone}", from.StartsWith("+1") ? from[2..] : from);
        
        Log.Information($"The final prompt: {finalPrompt}");

        return (assistant, knowledge, finalPrompt);
    }
    
    private async Task ConnectOpenAiRealTimeSocketAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistant, string prompt, CancellationToken cancellationToken)
    {
        _openaiClientWebSocket.Options.SetRequestHeader("Authorization", GetAuthorizationHeader(assistant));
        _openaiClientWebSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

        var url = string.IsNullOrEmpty(assistant.ModelUrl) ? AiSpeechAssistantStore.DefaultUrl : assistant.ModelUrl;

        await _openaiClientWebSocket.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);

        await SendSessionUpdateAsync(assistant, prompt, cancellationToken).ConfigureAwait(false);
    }

    private string GetAuthorizationHeader(Domain.AISpeechAssistant.AiSpeechAssistant assistant)
    {
        return assistant.ModelProvider switch
        {
            AiSpeechAssistantProvider.OpenAi => $"Bearer {_openAiSettings.ApiKey}",
            AiSpeechAssistantProvider.ZhiPuAi => $"Bearer {_zhiPuAiSettings.ApiKey}",
            _ => throw new NotSupportedException(nameof(assistant.ModelProvider))
        };
    }
    
    private async Task ReceiveFromTwilioAsync(WebSocket twilioWebSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 10];
        try
        {
            while (twilioWebSocket.State == WebSocketState.Open)
            {
                var result = await twilioWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                Log.Information("ReceiveFromTwilioAsync result: {result}", Encoding.UTF8.GetString(buffer, 0, result.Count));
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _openaiClientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Twilio closed", cancellationToken);
                    break;
                }

                if (result is { Count: > 0 })
                {
                    Log.Information("ReceiveFromTwilioAsync result: {@result}", JsonConvert.DeserializeObject<object>(Encoding.UTF8.GetString(buffer, 0, result.Count)));
                    
                    using var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(buffer.AsSpan(0, result.Count));
                    var eventMessage = jsonDocument?.RootElement.GetProperty("event").GetString();
                    
                    switch (eventMessage)
                    {
                        case "connected":
                            break;
                        case "start":
                            _aiSpeechAssistantStreamContext.LatestMediaTimestamp = 0;
                            _aiSpeechAssistantStreamContext.LastAssistantItem = null;
                            _aiSpeechAssistantStreamContext.ResponseStartTimestampTwilio = null;
                            _aiSpeechAssistantStreamContext.CallSid = jsonDocument.RootElement.GetProperty("start").GetProperty("callSid").GetString();
                            _aiSpeechAssistantStreamContext.StreamSid = jsonDocument.RootElement.GetProperty("start").GetProperty("streamSid").GetString();

                            _backgroundJobClient.Enqueue<IMediator>(x=> x.SendAsync(new RecordAiSpeechAssistantCallCommand
                            {
                                CallSid = _aiSpeechAssistantStreamContext.CallSid, Host = _aiSpeechAssistantStreamContext.Host
                            }, CancellationToken.None));
                            break;
                        case "media":
                            var media = jsonDocument.RootElement.GetProperty("media");
                            
                            var payload = media.GetProperty("payload").GetString();
                            if (!string.IsNullOrEmpty(payload))
                            {
                                var fromBase64String = Convert.FromBase64String(payload);
                                
                                _wholeAudioBufferBytes.AddRange([fromBase64String]);
                                
                                var audioAppend = new
                                {
                                    type = "input_audio_buffer.append",
                                    audio = payload
                                };

                                if (_shouldSendBuffToOpenAi)
                                    await SendToWebSocketAsync(_openaiClientWebSocket, audioAppend, cancellationToken);
                            }
                            break;
                        case "mark" when _aiSpeechAssistantStreamContext.MarkQueue.Count != 0:
                            _aiSpeechAssistantStreamContext.MarkQueue.Dequeue();
                            break;
                        case "stop":
                            _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x => x.RecordAiSpeechAssistantCallAsync(_aiSpeechAssistantStreamContext, CancellationToken.None));
                            break;
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Error("Receive from Twilio error: {@ex}", ex);
        }
    }

    private async Task SendToTwilioAsync(WebSocket twilioWebSocket, CancellationToken cancellationToken)
    {
        Log.Information("Sending to twilio.");
        var buffer = new byte[1024 * 50];

        try
        {
            while (_openaiClientWebSocket.State == WebSocketState.Open)
            {
                var result = await _openaiClientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                Log.Information("ReceiveFromOpenAi result: {result}", Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result is { Count: > 0 })
                {
                    Log.Information("ReceiveFromOpenAi result: {@result}", JsonConvert.DeserializeObject<object>(Encoding.UTF8.GetString(buffer, 0, result.Count)));

                    var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(buffer.AsSpan(0, result.Count));

                    Log.Information($"Received event: {jsonDocument?.RootElement.GetProperty("type").GetString()}");
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "error" && jsonDocument.RootElement.TryGetProperty("error", out var error))
                    {
                        Log.Error("Receive openai websocket error" + error.GetProperty("message").GetString());
                    }

                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "session.updated")
                    {
                        Log.Information("Session updated successfully");
                    }

                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.audio.done")
                        _aiSpeechAssistantStreamContext.ResponseStartTimestampTwilio = null;
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.audio.delta" && jsonDocument.RootElement.TryGetProperty("delta", out var delta))
                    {
                        Log.Information("Sending openai response to twilio now");
                        
                        var audioDelta = new
                        {
                            @event = "media",
                            streamSid = _aiSpeechAssistantStreamContext.StreamSid,
                            media = new { payload = delta.GetString() }
                        };

                        await SendToWebSocketAsync(twilioWebSocket, audioDelta, cancellationToken);
                        
                        if (_aiSpeechAssistantStreamContext.ResponseStartTimestampTwilio == null)
                        {
                            _aiSpeechAssistantStreamContext.ResponseStartTimestampTwilio = _aiSpeechAssistantStreamContext.LatestMediaTimestamp;
                            if (_aiSpeechAssistantStreamContext.ShowTimingMath)
                            {
                                Log.Information($"Setting start timestamp for new response: {_aiSpeechAssistantStreamContext.ResponseStartTimestampTwilio}ms");
                            }
                        }

                        if (jsonDocument.RootElement.TryGetProperty("item_id", out var itemId))
                        {
                            _aiSpeechAssistantStreamContext.LastAssistantItem = itemId.ToString();
                        }

                        await SendMark(twilioWebSocket, _aiSpeechAssistantStreamContext, cancellationToken);
                    }
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "input_audio_buffer.speech_started")
                    {
                        Log.Information("Speech started detected.");
                        var clearEvent = new
                        {
                            @event = "clear",
                            streamSid = _aiSpeechAssistantStreamContext.StreamSid
                        };

                        if (_shouldSendBuffToOpenAi) 
                            await SendToWebSocketAsync(twilioWebSocket, clearEvent, cancellationToken);
                        
                        if (!string.IsNullOrEmpty(_aiSpeechAssistantStreamContext.LastAssistantItem))
                        {
                            Log.Information($"Interrupting response with id: {_aiSpeechAssistantStreamContext.LastAssistantItem}");
                            await HandleSpeechStartedEventAsync(cancellationToken);
                        }
                    }

                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "conversation.item.input_audio_transcription.completed")
                    {
                        var response = jsonDocument.RootElement.GetProperty("transcript").ToString();
                        _aiSpeechAssistantStreamContext.ConversationTranscription.Add(new ValueTuple<AiSpeechAssistantSpeaker, string>(AiSpeechAssistantSpeaker.User, response));
                    }
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.audio_transcript.done")
                    {
                        var response = jsonDocument.RootElement.GetProperty("transcript").ToString();
                        _aiSpeechAssistantStreamContext.ConversationTranscription.Add(new ValueTuple<AiSpeechAssistantSpeaker, string>(AiSpeechAssistantSpeaker.Ai, response));
                    }
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.done")
                    {
                        var response = jsonDocument.RootElement.GetProperty("response");

                        if (response.TryGetProperty("output", out var output) && output.GetArrayLength() > 0)
                        {
                            foreach (var outputElement in output.EnumerateArray())
                            {
                                if (outputElement.GetProperty("type").GetString() == "function_call")
                                {
                                    var functionName = outputElement.GetProperty("name").GetString();

                                    switch (functionName)
                                    {
                                        case OpenAiToolConstants.ConfirmOrder:
                                            await ProcessOrderAsync(outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                        
                                        case OpenAiToolConstants.ConfirmCustomerInformation:
                                            await ProcessRecordCustomerInformationAsync(outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                        
                                        case OpenAiToolConstants.ConfirmPickupTime:
                                            await ProcessRecordOrderPickupTimeAsync(outputElement, cancellationToken).ConfigureAwait(false);
                                            break;

                                        case OpenAiToolConstants.Hangup:
                                            await ProcessHangupAsync(outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                        
                                        case OpenAiToolConstants.AddItem:
                                            await ProcessAddNewItemsToOrderAsync(outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                        
                                        case OpenAiToolConstants.RepeatOrder:
                                            await ProcessRepeatOrderAsync(twilioWebSocket, outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                            
                                        case OpenAiToolConstants.TransferCall:
                                        case OpenAiToolConstants.HandlePhoneOrderIssues:
                                        case OpenAiToolConstants.HandleThirdPartyDelayedDelivery:
                                        case OpenAiToolConstants.HandleThirdPartyFoodQuality:
                                        case OpenAiToolConstants.HandleThirdPartyUnexpectedIssues:
                                        case OpenAiToolConstants.HandleThirdPartyPickupTimeChange:
                                        case OpenAiToolConstants.HandlePromotionCalls:
                                        case OpenAiToolConstants.CheckOrderStatus:
                                            await ProcessTransferCallAsync(outputElement, functionName, cancellationToken).ConfigureAwait(false);
                                            break;
                                    }

                                    break;
                                }
                            }
                        }
                    }

                    if (!_aiSpeechAssistantStreamContext.InitialConversationSent && !string.IsNullOrEmpty(_aiSpeechAssistantStreamContext.Knowledge.Greetings))
                    {
                        await SendInitialConversationItem(cancellationToken);
                        _aiSpeechAssistantStreamContext.InitialConversationSent = true;
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Information($"Send to Twilio error: {ex.Message}");
        }
    }
    
    private async Task ProcessOrderAsync(JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        _aiSpeechAssistantStreamContext.OrderItems = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(jsonDocument.GetProperty("arguments").ToString());
        
        var confirmOrderMessage = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = $"Please confirm the order content with the customer. If this is the first time confirming, repeat the order details. Once the customer confirms, do not repeat the details again. " +
                         $"Here is the current order: {{context.OrderItemsJson}}. If the order is confirmed, we will proceed with asking for the pickup time and will no longer repeat the order details."
            }
        };

        _aiSpeechAssistantStreamContext.LastMessage = confirmOrderMessage;
        
        await SendToWebSocketAsync(_openaiClientWebSocket, confirmOrderMessage, cancellationToken);
        await SendToWebSocketAsync(_openaiClientWebSocket, new { type = "response.create" }, cancellationToken);
    }

    private async Task ProcessRecordCustomerInformationAsync(JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        var recordSuccess = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = "Reply in the guest's language: OK, I've recorded it for you."
            }
        };
        
        _aiSpeechAssistantStreamContext.UserInfo = JsonConvert.DeserializeObject<AiSpeechAssistantUserInfoDto>(jsonDocument.GetProperty("arguments").ToString());
        
        await SendToWebSocketAsync(_openaiClientWebSocket, recordSuccess, cancellationToken);
        await SendToWebSocketAsync(_openaiClientWebSocket, new { type = "response.create" }, cancellationToken);
    }

    private async Task ProcessRecordOrderPickupTimeAsync(JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        var recordSuccess = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = "Record the time when the customer pickup the order."
            }
        };
        
        _aiSpeechAssistantStreamContext.OrderItems.Comments = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(jsonDocument.GetProperty("arguments").ToString())?.Comments ?? string.Empty;
        
        await SendToWebSocketAsync(_openaiClientWebSocket, recordSuccess, cancellationToken);
        await SendToWebSocketAsync(_openaiClientWebSocket, new { type = "response.create" }, cancellationToken);
    }
        
    private async Task ProcessHangupAsync(JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        var goodbye = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = "Say goodbye to the guests in their **language**"
            }
        };
                
        await SendToWebSocketAsync(_openaiClientWebSocket, goodbye, cancellationToken);
        await SendToWebSocketAsync(_openaiClientWebSocket, new { type = "response.create" }, cancellationToken);
        
        _backgroundJobClient.Schedule<IAiSpeechAssistantService>(x => x.HangupCallAsync(_aiSpeechAssistantStreamContext.CallSid, cancellationToken), TimeSpan.FromSeconds(2));
    }

    private async Task ProcessAddNewItemsToOrderAsync(JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        var recordSuccess = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = jsonDocument.GetProperty("arguments").ToString()
            }
        };
        
        await SendToWebSocketAsync(_openaiClientWebSocket, recordSuccess, cancellationToken);
        await SendToWebSocketAsync(_openaiClientWebSocket, new { type = "response.create" }, cancellationToken);
    }
    
    private async Task ProcessTransferCallAsync(JsonElement jsonDocument, string functionName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_aiSpeechAssistantStreamContext.HumanContactPhone))
        {
            var nonHumanService = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "function_call_output",
                    call_id = jsonDocument.GetProperty("call_id").GetString(),
                    output = "Reply in the guest's language: I'm Sorry, there is no human service at the moment"
                }
            };
            
            await SendToWebSocketAsync(_openaiClientWebSocket, nonHumanService, cancellationToken);
        }
        else
        {
            var (reply, replySeconds) = MatchTransferCallReply(functionName);
            
            _backgroundJobClient.Schedule<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
            {
                CallSid = _aiSpeechAssistantStreamContext.CallSid,
                HumanPhone = _aiSpeechAssistantStreamContext.HumanContactPhone
            }, cancellationToken), TimeSpan.FromSeconds(replySeconds));
            
            var transferringHumanService = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "function_call_output",
                    call_id = jsonDocument.GetProperty("call_id").GetString(),
                    output = reply
                }
            };
            
            await SendToWebSocketAsync(_openaiClientWebSocket, transferringHumanService, cancellationToken);
        }

        await SendToWebSocketAsync(_openaiClientWebSocket, new { type = "response.create" }, cancellationToken);
    }

    private (string, int) MatchTransferCallReply(string functionName)
    {
        return functionName switch
        {
            OpenAiToolConstants.TransferCall => ("Reply in the guest's language: I'm transferring you to a human customer service representative.", 2),
            OpenAiToolConstants.HandleThirdPartyDelayedDelivery or OpenAiToolConstants.HandleThirdPartyFoodQuality or OpenAiToolConstants.HandleThirdPartyUnexpectedIssues 
                => ("Reply in the guest's language: I am deeply sorry for the inconvenience caused to you. I will transfer you to the relevant personnel for processing. Please wait.", 4),
            OpenAiToolConstants.HandlePhoneOrderIssues or OpenAiToolConstants.CheckOrderStatus or OpenAiToolConstants.HandleThirdPartyPickupTimeChange or OpenAiToolConstants.RequestOrderDelivery
                => ("Reply in the guest's language: OK, I will transfer you to the relevant person for processing. Please wait.", 3),
            OpenAiToolConstants.HandlePromotionCalls => ("Reply in the guest's language: I don't support business that is not related to the restaurant at the moment, and I will help you contact the relevant person for processing. Please wait.", 4),
            _ => ("Reply in the guest's language: I'm transferring you to a human customer service representative.", 2)
        };
    }
    
    private async Task ProcessRepeatOrderAsync(WebSocket twilioWebSocket, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        _shouldSendBuffToOpenAi = false;
        
        var holdOn = _aiSpeechAssistantStreamContext.Assistant.ModelVoice == "alloy"
            ? "UklGRtxFAABXQVZFZm10IBIAAAAHAAEAQB8AAEAfAAABAAgAAABmYWN0BAAAAIhFAABMSVNUGgAAAElORk9JU0ZUDQAAAExhdmY2MS4xLjEwMAAAZGF0YYhFAAD+/v7+/v7+/v7+//////////9+fn5+fn5+fn5+fn5+fn5+fn59fX5+fn19fX19fX19fXx8fHx8fHx8fHx8fH18fH18fHx9fX19fX19fX1+fn5+fn5+fn5+fX7///7//v7+fn1+////fv9+fX18fH7+/P3+fn5+/v59fX78+vz9//79+/n6enJ1e/z6/f37/P59fHx7/vr/enh1en3z7vp++Pf+d29xcW9tYWrt3990ZHpsefJ37l3sxtH8bUlOv1bZUkXj0M3PZkl2VHZgdn7M2d95UFtwaeXea975YV1VT1tc1cDL0nRPT1zf0tTV6efkWkdITuO+wNDlUExQTF1uYu/o3NV6YF9c/+dr5u5v3XZTW05atWtSTUHFuMHOW0HK1G9POkbIv7rMSEtZ3sfYXGVn9V9DPkdT5/VMQDk0LiopLj3VvaymtsdHXL2rn6GqrrrAv2RPeMyyrsRNOTpm3OdmTltgTUE5PEhTTz02NTk6OTUxNTMtKysuNkDerKSlrdtTzbitrbW3sK+zx1VNa8vDzvFraWxfR0JJYONeQz0/Rkg/PD0/Qjo1MS4yNTk7PLmlnp+4UE7Vr6asr7CxrrjmVE1rwL3B0FZQV0tJQ0NSVE5GOzo7ODc2NDg3Mi0rKy0wOzTdp56crVA9Vrajp6+2uK6vxWFFW767vtNHVGteTTw5S2NySzs5O0A+OzQyMjIvLCotOkA00aqfnK54RlGwoquuubmssL3rQ1bOwL/aV19eUz46PkhqWUZAPT5AOzUwLzEvLSssMT04PbamnaG8VFXEqKSrr7myrbTLWEfxwrzFe1FPWEo/PkBOW1JFPj0+PzoyLi8uLSopLjY9NOirn52t6EZdsaKmq7e6sK+98z5Jzrq2ylVEQkVKQkdHTltNSkE6Ojg0My8tKygpLjhBOD+uoZym80JMuaGjq7a+say20EJA0r20w1pFSExQRD5CTXhkTD86Ojw2MC0sLS0rLTI4O0GuoZ2k20VLwKSiqq++ta63x0pDdMO2vP5GP0BMR0JDSFlkTkI6Nzs5NC8qKisrLjU5Ozywn52h2kNKwqShqq/Ct662yUo/YL+2vPlCPkdTUEdCRVd+VkY7Nzw5NC4qKiorLTU8Pjyzn52fzURIx6aiq6/Ata21y0c9Zb21vmpFRE9dSj8+RmThWz84Njw9NS0qKiosLTI4QD+7n56ev0RC1qmgqa7CvK6zvVA9Tsa1uN9EPEVbWEQ8PU/wb0Y5NTo+OjAqKSosLjI4PDrHn52cuD8/bqmfp67Evq2vuVI5R86ytttFOEVnaks6O0l270s6NTg+OzApKSotLi4zOT3Vn52csD9BXa2hpq29vq6wuFs5Qt63s9JNN0Bda0w8OkZz4F89ODY8OjIrKSosLi80Nz1goZydqj4/W6+gpK65xq2uuPA2Pt+5sc1LOjxha0w7Nj5i7Ws/NzY5OjMrKSkrLTA4Oz9Wop2cpz8+TbGfoau5zq+wtdo0PF65sMJQODZPcFY/OD1U52hENzU5OzMtKSkrLS83OkRRpZyco0Q6Q7ifn6m20bWut8w4OVi7rrxaOTRHZFZEOT5R/W9FODc4OjMtKyssLSwzO0xVsJ2dnso+QM+lnqOtxsCytr1MOEXPs7bUPzM5S1ZKOztGXXhNOzQ0NzcwLCopKy0yP0tczaGcnKxAPUqtnp2outm6trjaOTpVvLC+TjUxPU5MQjo+TWFWQjczNDUxLSsqKiwxPlbs1aWcnaVLO0a0npyks+jIt7jDPTZHxK6zYzUuN05aSDk5QmtwSjYvLzIwLiwoKSovRuHFwaSen6lbQVa2npyisGjdxLq/TztIyrCwajgrMUhoXjw3PFZ8VjkvLC0uLy4pKigxVsG8tqWgoat0Rnm0nZ2jt01xw7rARjhJyq60UDUpM01wVTo0PVbuVzgvLC0uMC4sKigy27evrKekqa5UbturnJ2mxkBYxr3EQjtLwa+8Qy4oM1vuUzYwO2HVXDksKiwwNzAsKCY/ta6mrainrbhq7LuinJ6zXzxkvMLZPTvpuLXaMCgqP9ftQC8xQ9zcSC8oKi86OS4oJCq7rKSnsamusL3YxKuinanEVD/QyNZePlLBvMc9LCs1WftJNDA7+dRYNSkpLzg9MCgjI9ipoaG0s7CvsLzNtKmforHXQUvb0t9cTNXDyFMyLDBBWEw4MThT1l85KykvOz44KSQgPKqhnq69urCtrb+8sqegqblVQFrdzd1UYtrT6z0wLjhITUA0NUBmb0EvKy07QDwsIx8os6ObpL3Cva+ps7y4rqGkrdE8QF/Oy3RYauvmSjYwMj5JSDk3PU1kSzcuLjU/PTMnJCBUp52bsMLMuqqrt7m7qaGotUo3QnTFzF1XW+jvQjYuMz9JRj05Q1BUPzEuMjw+OCwmICi2o5qiu8PHrqmvub60pKWqyzw5Q93J21VSWOlZPjIvMz5IREA9SE5JPDExNz09MiolITyqnpqrvc69q6qxvL6to6avUzU3S87OaUxNce9LOC0uNkNPRTw/SVZKNzAxOkc9MCkjJc+jm5yzw8mxqKu5wbyooqi9OzM9bcnjS0VO9mg/MCsvPEpMPTtCUlxCNDA1Qkc8LigjK7GgmqG5xL+tqK+9vrSkpKzcNzdD6NdWREpe7k03LCwzP0tBPj9QZ1A9MjI6SkY6LSkmOaqfmqa6u7arqba8vK6jqLNONjlL6WhIPk5q7EUxKiw1QkpBP0BTXEU6MzY+S0M2LSopVKefnKm1sLGur8a5taikrsdBPEZrWEU4Rn3O4TwtKS85SURAQ0tcTDw3NjtFRTkxLi48xaegoKmsqrq5ysCtqaWvyktMVtxQOzQ91sLOQC0pLzlCPzg+UGNdPjQ3OklHOTIvMkTir6ako6Wjt9JbzqujpLHnRFrbzEw0Lz3NuspALCovO0E4NTlLcFdANjY/SUg4Ly81TMy0qaOlnqG65DfJqqGkt1JJWsrMQDAsQsK1yzsrKjM+PjMwN03pYD8zNT5PSzowMD10vK6oo6Sbo75VNL2ooKfFQ0ptwvM6Li9dvLd0NCosOEA7Mi86UW9VOjM1P09JOjI2RdS6raiioJulzEc3t6airNpBUOHHVTItNd23ulcyKy86PTYvMDxZaEs3MjdETkg4NTpQyLitqKKemqjTOzqypqKwaj5O3cxHLyw4zLa9SS8rMjw9NC0xP2hrRzUyN0tbSDg0PF2/ta6qpJ2Zptc8OLOopbJSPUnu1UEuLDjMuL5HLywxPj03LjI/ZOhMOTU6TGVLPDY/+b61r6yjnZih0T01u6ims147SGDxPSwsN9e6wE4yLjI6OjQuMj9cd049Oj1MUkc8O0jevriyrqSdmZzCRzfJqqew6TxEWFxALCszeL7FUTQvMzs6Mi4xPVRkSj48Qk9YST49TODCubWupZ2amrRWPGStqq3QQEFOTkAsKzBQw8paNzA1OjgwLS89V2JMPj9JXGFIPz9P1MK8t7KnnZqZrF5ETbGrrspHP1FKPywqMEnHzVw5NDg7OC8sLz5bYko9P099dko+QFPOwL26squdm5mlfFVMua2vxlY9SUI6LigwR87QUzc1Oz85LiwuPmRpSTw+VOHmUkBBXcy/v761rqCbnJ/E6vTHs7XHeUlCQTItJzBNzM1PNjM+RD4uKCw86NJROjhF7tpmRT9Zyru7v7ewppydn7HRwMS3ucnwYkY+LSklLXjIvUwzLjtLRC8oKjnYyWs7NTtk3O1LQFTPvLm9uLKpnp6hrb67t7e6x+1yTDstJSQodry23C8tNEtQNScnL9q7yUQyMkTk4F1CTNW7tLi7t62inqCptru4tbnF4GRUPS8lIyM7ure6OSwvPUw+KSUqR7u87TkyPGPd/khI7Lywsbe6sKafoKi0u7i1tsHdZ1Y/MSUhIS++uLVALC85SjwpIypAurrSPjU9WutoSkzVuq+ytriupJ+iqrW6tLK1x/NgVD4uIx8fM8K1tDwtLThJOyklKkO8u8tANzxV5ndPVtG4rq+zta2kn6GptLm2tLjKbVVIOi0jHx45x7G4Oy8tOkM5KCYqTru6yEM6PVfwbVFlyrWtr7Czq6ShpKy2ure3vdVXSz02KiMeI0rGrdM6Ly88QDAoKC7gvbnSST5HXmleVt6+r62usq+po6Knr7m7uLnD804+OS8nIR0pULuv+zsuMDk9LiopNNq7udNLP0td5/dv1L+yrq6urKikpKmvtrm6vsvxTj01LCUgHSxOu7R9PzAzODouKyo17764ymFKVGfmcW7WwrKura2rqKWmqq+1uLq/z+5MPjYsJSAdKkbFtetGNjc6PS8rKjBbxLnD31xm6ur4YdrEtq+trauopqWqrbO4u7/P/Es8Ni0mIB0kO+C61k07Ozw/NCsrLUXTvL7N5uzX3ONi+c+8s66trKimpKessLe6vcr0Tjw4MCojHh4rS8zAZT87PD4+LysrMVLKvcPM3c3M1dpp5cm7ta+uq6ako6essbi8xNxZRjw3MSojHx8tQn3XSkFAQ01CMy0sMU3bx8bMy8PAxcrg18u/uLSwrKilo6arr7a8xd9RQjo4MywnISAnNERVSD9FSllXPzUxMj5d4M3NycC8vL7FzMfDvrq4s62pp6aprLC3vMtySD03NC4qJSAiKTI8QD5ASlV+YEg9OTtGWn3c1srAvLq7vb+9vry8urWwrKqpq6yvs7nF4U8/NzEtKSUhIiYsMDU3PEVV8+hpUEpJUWH/4dnNw7y6uLm6ubm4ubm4tLGvrq+wsbS4vsz6TD43MCwoJSIkJyksLTA3Q1ro3d/d29LOzMzNzcrFwr/Av768urm3tbOysLCwsbK1uL3G12VMPzcvKyclJCQmJygqLjdAT1x24tDJxMC/v8C+vby8vLy7urq5uLa0s7Ozs7O1t7m+x9piSz42LiomJCMkJiYoKzA5RlRl7djKxcHAv7+/vr69vr69vb29vLq3tbOysrKysrS3u8TTcE0/Ni4pJiQjJCUmKCswOkVRYPXYysXBv76+vr29vb6+vb69vby6uLW0tLSztLS3ur7K4VpHPDQsKCYkJCUmJyotNT5LXHjgz8bDwL+/vr69vr6/vr6+vr68uri2tLS0tLW2uLvAze5VRTsyLCgmJCQlJygrLzhCT2nq2MvDwcC/v769vb6/v769vb29vLq3trS0tba3uLq9w9JzTkE5MSsoJiQkJScpLDA5RFJw4tPJwcDAv7+9vLy9v8C+vby8vLy7uLa1tba3uLm7vcPReU9AOTErKCYkJCQnKSwxOURRbeHQyMHAwb++vby7vL6/vr28vLy9u7m3trW2t7i5u73CzvRRQjoxLCgmJSQkJiktMjlDT2rez8jCwMHAv768vL2/wsG+vb2+vr27uLa2tre4uLm7v8ndX0g8My0pJyYkJCYpLDI5QEtc5tPKxMLDw8G/vr2+v8PCv729vb69u7i2tbW2t7i5u77G2GdKPTUuKignJSQmKSwxOD9KWvHWzMbExcXDwb++vsDCw8G/vb2+vry5uLa2t7i4ubu+xM/1UkE5MCwpKCclJigsMDc9RE9n4dLLxsfHx8TBv7/BwsTCv769vb29uri3tre3uLm7vcLL4FtGOzMtKikoJiUnKi41O0BKXO/VzcfHyMfFwsC/wcLDw8C/vr29vbu5t7a2t7e4ury/xdH8Tj83LysqKScmJiktMjg9RFBx3M/Kx8nJx8TBwMHDxMTCwL++vr68u7m3t7e3t7m7vcLK211GOzMtKyooJiYoKzA2O0BLXuvVzcnKysjGw8LBw8XFw8HAv76+vby6uLe3t7e4uby/xs/8TT42LisqKScmJyouNDk+RVJz3NDLycrIxsTCwcLDw8LBwL+/vr69vLq5uLe3t7i7vsLJ2F5FOjItKyooJycpLDE3PEFLXuvVzcnJyMfFwsHBwsPCwcDAv769vby7urm4t7e3uby/xczpTz41LysqKScmJyotMzg9Qk1j59XNy8rJx8bEw8TFxMPBwcC+vb28u7q5uLa2tre5vMHH02VFODEtKyooJycoKy81Oj5GU3bdz8zJycfFxMPDw8TCwMDAvr28vLy7urm3tbW2t7q9w8vfUz40LywrKSgnKCstMjg8QUpc8djPzMrJxsXEw8PCw8G/v769vLu7u7q5t7W1tra4u7/G0HRIODAuLCooJycpLC81OT1DTmXh0s3LycjGxcXFxcXFw8HAv728vLy7u7m3tra2t7m8wMjXXkE1Ly0sKSgoKCotMTY6P0dUdtzRzsvJyMbHx8fHx8XDw8G/vby8vLy7ube2tra3uby/yNVfQjYvLiwqKCgpKy4yODtASVj928/NysjHxsfHyMjIx8XGxMK/vr29vby7uLa2tre5vL/H1WNENzAuLCopKSkrLjI3Oz9IVG3i08/MysnIycnKycrJx8fFw8C+vby8u7q3tbW1tbe7vsTPckg5MS4tKykpKistMTc7P0dRZurWz8zMysrLysrKy8rJyMXDv769vLu6uba1tLS0tbm9w8zyTDszLy0rKikqKy0wNjo/RU1bdd7Uz8/Ozc7MzM3NzMvKx8TAvr28u7q5t7W0s7OzuLzByN9SPjUvLSwqKiorLS81Oj5ES1Zp6NnS0dHQ0c/Oz9DPzs3JxsPAv728urm3tbSzsrK2u7/G1VxDNzEuLCsqKystLzM5PUFHTlt349vY2djY1NHT1NPRzsrHxcG/vbu6uba0s7KwsLS5vcLNcEk6My8tLCsrLC0vMzk9QUZMVmjv49/i4d/c2Nnb2tbQzMjFwr+9u7m4tbOysK+vsbe7v8rzSzw0MC4tLCwsLS8zODw/QkhOW2p4fnl8+e3k4eTi29PNycbCv726uLe0srCvrq6wtbm9yOxMPTYyLy0sLC0uLzQ4Oz5BRk1XXmhnZ2pue/Tr7+nf1s7Kx8O/vbq4trOxr66urrC0uLzI7Uw9ODMvLS0tLS4xNTg8PkFGTFRbXlxdYGZseH197+DXz8vHw7+8uri2s7Curq6vsLO4vcx2ST04MzAuLS0uMDQ3OTw/QkhNVFhZV1peYmZpbHXv49rQzMjDvry6uLWyr66urq+ws7m/0GZGPDg0MC4tLi8yNTg6PT9ESU1SVFRUV1lcXl5fZ3nu39bPysS+vLq4tbGura2urq+xt77Nb0s+OjUxLi4uMDM1Nzk8P0NITE9PUVJUVlhaWVxibv7m2dHLxb68ure1sK6tra6urrC2vMrwT0A7NjIvLi4vMjU3OTw+QkZJS0xNTU5PUFNUV11mc+7d0svFv7y6t7Wwrqytrq6ur7S7x95XRT04NDAuLi8yNDY4Oz1AREhKSktMTU5PUVNXXWdx79/VzMa/vLm3s6+trKytra2vtLvH31ZGPjg0MC8uLzEzNTc6PT9DR0hJS0xNTk5QU1leZW724tfNx8C9urezr62sra2urrC2vcrlWEk+OjUyMDAxMzQ2ODs9P0FERUdHSEpMTlBUWmVz/Ove1MzFv7y6trGurKytra2usri/zfFURj45NTEwMDEzNTc5PD5AREZGRkdJS0xOUVhhbXry4NjQysK+u7i0sK2sra2ur7G2vMbaZU1EPTg0MjEyMzU2ODs9PkBCQkJCREZISkxQWGRz+end1MzFv7y5tbCurKysra6vsri/zexYST86NjMxMTI0NTc5Oz0+P0BAQEJER0pNUVpo/u3i2dHMxb+8ubaxrq2tra2ur7O4vsnfX01DPTg1MzMzNDY3OTo8PT4/Pj4/QkRGSk9YZHjw49jRzcfAvLm2sq+trK2trq+yt73I3GJNRD04NTMyMjM0Njg5Ojw9Pj4+P0JFSExPWWZ+7eTa1M7Iwby6trKvraysra6vsba7w9H2WEpAOzc1MzM0NDY3ODk7PDw8PD4/QkVJTVRdbPPk3NfPyMG9urezr66tra2ur7C0ucDM4mBMQjw4NTIxMjM0NTc4Ojw9PT4/QEJFSEtPV2Bt9+Xd1s3HwLy5trKvrq2sra6vsbW6wM3kXUtAOzg1MzEyMzQ1Njg5Ozw9Pj9BREhMUFhhc+ze19HMx8K+u7i1srCvrq6ur7G0uL3F0e9ZSkA8ODUzMjIyNDU2ODk7PD4/QENGSU1SWmN07t3Vz8vHwb67uba0srCwsLCxtLe6vsXP5l9NRj47ODY1NDQ0NTY2Nzk6Oz0/QkZKT1dk/OXZ0czHw7++u7m4trW0s7O0tba4u77FzNj7Wk1GQD06OTg3NzY3ODk5Ojw9P0FESEtOU11u7t7Z0MzHw8C+vbu6uLe2tre3uLm8v8TK0eJuWU5HQj89PDs6OTo7Ozw8Pj9BREdJS01RWWV47eTb087JxsPBv768urm4uLm6u7y+wcjP2/FiVExHQz89PDw7Ozs7PD09PkBDRUlMT1VbZ3Px49vTz83MyMTDwL++vr69vby9v8LFx8zQ2uxtXFZPTUhEQUBAPz8+Pj9BREZISk1RWF9p/u3h2tPOzs3MysnKycnJysrKysvNztHW2t/k7XRkXFtXU1BPTkxLS01OTU1NUFJUV1peYWdv+Ovn5eTc2tra2dfW1djX1dbX2tjZ3eLq6O/3d2dpY2BcXGFdXVtgZmNmZGhqbm1u+fT5+fDp5+jn5+fn6+np5erv6Onm7O3r7O367u71+3719vh5cHn9fHBwdXNsa2xtcG1ra3N5ent3+vb6+vvw8/n7+ezs7/j07Pb7fP7+bmtscm1naGpwbm1sb31xbW11eHB1d3x6eXx0/PZ8dnV8d3l5dX16end59vv7/f749/X9/P16eXF1d3Fwb3BucG5qbnBxb3F7fP7//vvz7/v68e3s7e3t7O3u7/L1/Hx5enpycHR3c3J5d3N0d315e317///7fn37env/+/r69v378vL2+vX09O/z+Pfx8ff4+fv+/vz9/v//fn7+fXl3eXd4dnR4dXd6fHt0eHp3dXN1c3NycHl4dHR6+vz7+/jx9PLw8PL38/T3+P39+vb4/Pn3+Pz8+/18ent7fv7+/vr1+fz9+/j6+/38+f39/vz6fnx5eXZydXFxcG9ua2xtbG1sbm9wcnB0eHt8e/99fv779fXz8/Pz9vf5+fx7dnR3dnh+/Pr8+vj18vL09/f08vHx7evq6+7u7e3t7/Dv7/Dv7Orq7Ozt7u3u7u/x8vb3+vz7+vt+fX17fH37/H17eHp5eHx8fHh3eHl7eXh4d3VxdHt9fHl6end3eHh1cHBwcXJydHV2c3Bwbm5ub3BvcXBxdXVzcG9ubG1wcnFyeH359PLy9PP1+fv9/n18fv/8+fb29/b19fX18/X49/Pw7u3v8fHx8/b4+v97enl5en19ff36+/v28vLz9PX18/L09fb4/P38/np2c3Bubm9wcXFydnv+/Pz69/X19fX09PX19fX3+vz+fXp3dHN0dHNzd3p8//v39fX09PPz9/x9fHt5dnNzc3BvcHRyb3B2eXd4fP38+vn59/b5+/v6/P9+fv9+fHp6fHt5eXt8fHp6e319fH3+/Pr5+fr49vf5+fj6/f7/fnt6eXh5eHZ2d3h2dnd4eXp8fX7+/fv6+vr6+v18enl4dXJxcXJyc3Z5e339+fb29vTy8fHx8fLy9PX2+Pv+fnx6e3x8fH39+/n49/Ty8fL08/P19/j4+Pn5+vr8/n59fXx6d3VzcnFxcG9vcHBwb29wcHFydHd4eHh5e3t7e3x9fX19fX5+fn19fHp4d3d3d3Z1dXZ3eX3+/v79+/j39/b19vj6+fn6/Pz7+vv6+Pf29fX08/Hx8PDw8fLy8/P09PT08/T2+Pf3+Pn6+fn6+vr39PP09fTz9PX3+v3/fXx7eXd0c3JxcnJycnJ0dXh6fHt5ent8e3t7fH19fv77+/z8+vr7/Pz9/f99fX59e3p5eXl6enp6enl5eXp6e31+/vz6+Pb09PX29vf6/X18enl2dXV2dnZ4ent8fH7//n5+fv9+fHt8fHp4d3d2dHJyc3Nzc3V2dnZ2eHl5eXp8fHt6e3x8e3x9fn5+/vv49vXz8fHy8vLx8vPz8vHy8vHw8PDw7u3t7e3s6+vq6unp6eno6Ojp6ejo6Onq6uvs7u/x9Pf5+/3+fn19fX18e3p7e3t8fHt5d3Z1dHJwb25ubWxramhnZmZmZmZmZ2hoaWprbG1tbm9vcHBxcnJycnJzc3NzdHV1dXV1dHNzc3NycnJzdXZ5fP/8+vn39vb3+Pn5+/v8/f3+fn5+//7+/f38+/r5+Pj4+Pj39fT09PX09PTz8/Ly8vLx8O/v7+/v7/Dw8vT29/f39/j5+/z8/P39/f39/Pv6+ff39fTz8vP09ff5+vz9/n59fHx9fv38+/r5+fj4+Pn6+/z9/n58enh3d3d4eHh4d3d4eHl5eXp6e31+//9+fXx7e3p6enp6ent8fX1+fX1+fv/+/f39/f39/f3/fXt5eHh5eXp6ent7fHx7enl4eHl6e3t8fHx9fX19fX19fX79+/n39vb29/j6+/3/fXx7e3t8fH19fv79/Pv8/P39/v5+fn5+fX5+fXx6eHZ1c3NycnJyc3R1dXZ2d3h5ent8fH19fv78+/n4+Pn6+/z8/f7+/35+fv/+/fz8/Pz8/Pv7+/v8/f5+fHx7e3t7e3t6enp6e33//fv6+fj39/b19fb3+Pn7/Pz9/f7/fnx7enp7e3x8fHx7fHx9fX18e3p6e3x8fHx7e3t7e3t7e3x8fX1+fn5+//7+/v7+/f39/f39/f7+/v7+fn59fX19fv//fn7//v39/Pv6+vn5+Pj4+fr7/Pz8/Pv7+/v7/Pz8/P39/f39/Pz7+/v8/Pz8/Pz+/359fX5+/v7//359fXx8e3t6ent7e3t8fHx8fX7+/v7/fn5+fv9+fXx6eHd2dnZ2dXR0dHV2d3d4eXp6e3x9fHx7e3t7e3t8fX19fn7//v7+/fz8+/r59/b19PX29/j5+vv8/f99fHt7e3p5eHh3d3d4eXp6e3x9fHx7e3p4dnRycHBwcHJzdHV2d3h4eXl5eXl5enx9fX19fX19fX18e3p5eXt9fX59fHx9fv7+/v7///7+/f7+fn5+//7+/f39/fz6+fn4+Pf29fTz8vPz8/Py8vPz9PX19fX19vf4+Pj4+fn7/P3+///+/v39/Pz7+/v8/P3+fn18e3p5enp6enl5eXl6e31+fn59fX19fX19fHt5eXl5eXh3dnR0dHZ3eXp7fHx9fv78/Pz8/f39/Pz8/f5+fXt6eXd2dnZ1dXV0dHR1d3p8fX5+//78+/v7/X58e3t8fX19fHt7e3t8fX18fHx8fX5+/v38/f38/Pz9/v99fXx9fX5+fn7//v7+/f3+/f39/Pz7+vr6+fr6+/v8/P39/v9+fv/+/v39/f7+/v7+/v7/fn5+fn5+fn19fXx7enp7fH7+/fz8/Pv6+fn6+/z7+vj29fX19/j4+fn6/P59fHx8fX7//v79/fz7+/v8/P39/fz8/P3+/35+fn59fXx7ent7fH1+/v79/fz7/P3+fXt5eHh4eHh3dnZ1dXZ2dXV2d3h6fH5+/35+fXx8e3p5eHh4eXl6e3t7fHx8fX1+//7+/fz7+/r5+fj4+Pf3+Pn6+/3+////fv/+/v39/f39/v7+/v38+/r5+fn5+Pf4+Pn6+/v7+vn6+vr6+vr6+vr7/f7+/v7/fnx6eHh5eXp6eXh4eHp8fX5+fn5+fv7+fnt4dnV1d3l6e3x8fv79/Pz9/f38+fb19PPz9fX3+fz+fHp5d3d5enp8fHx8e3t8fXt7enl6e3x+/f7+/v/+/v9+fn18fH7+/Pz7+vn5+fj4+Pr8/f99e3l5eHZ2d3h5ent7fHt7fH19fv/+/v7//v5+fX59fn59fX18e3x8e3t7e33+/fv6+vv7+/v7/P39/n7//v7+fn1+fn79/Pz8/f79/P38/P7+/f78+fr6+vz9/f39+/z9/Pz8+/r7+/z9/v3+/v5+fX17e3t8e3t8fH19fX5+fn1+fn7+/fz7+fn5+Pn5+fv7+vr6+Pb18/Hy9Pb6/X57eHZ0cW9ubW1ta2poZmRjYmJiY2RnbHP97+nj393c29vb29vc3Nzc3Nzd3+Tq8Px0bGVfXFpZWFdVUk9NS0pKSktMTlJYYnnm2dHMycbDwsDAwMLExsnMz9be63poX1tYVlVUUlFPTkxJR0VFRkhLTlVdaf7p39za2trc3d/j6vV3a2RgXVxaWFhYWl5oe+rd1M3Hwr68u7u8vb/CxsvR3fNnWlFMSUVCPz4+Pj4/QEJER0pOVFtlcPzt5N3Z1NDNy8nGxMPCwsPFyMvO0tjd4+36cmZbUkxHQ0A/QENHTFRfd+jd2NXW2d7rdmFYUExJRkRDQkJDRUhLUFtw49LJwr26t7WzsrGysrS2ubzAyNHnX05FPjs5ODk7PUJJUFxu9Ofh4+fue2pfWFFMSEM/PTw7Ozs8PT9ESlRw2svBu7e0sa+ur7CxtLe6vcPK0utfUEU+Ojc1NTY4PERNXejWzsjHycvP2/BjVkxGQz47Ozk4Ojo7Pj9ETVnw0MW8t7Ovrq2tra6ws7i8v8jS33BRSkE8ODY0MzU4O0JOX+jSzMnHyMvR3ndaTkdBPzw5OTg3OTo7PkBGTlvsz8S8t7Ovrq2sra6vsre7vsbP1exbUkk/PDo2NTY4OkBKVf/Wz8rGx8rN2PZhT0dBPTo5NzY2Nzg7Pj9FTVZz2Mq/urWwr62srK2usLK5vb/M2NphUE1COzo3NTQ3OTtET1zo0MzJxMbN0NtnVU1CPDs4NTU1NDY5PD1DTVJq1cvBuLWxrq2srKutr7C2vb7H2uNyTklCOzg3NDQ1ODtATFzw0srHw8LIzNX5WkxCPDk2MzMzMzU4Oz5ES1n/2Me+ubOwrqysrKusr7G1vMHG2PVuUUZCPTg3NjQ2OTxCTmPjz8nEwsLGy9TwW0xCPDk2MjIyMjQ4Oj5HTlzp0ce+ubSxrqysraytsLK2vMPJ0/VjWEtDPzw5ODc3OTxASVRz3M/Lx8fJzdbmZlBHPzs4NjMyMzQ2Oj1ETmDq0Ma/u7e0srCvr6+wsrW4vMDI0d34ZlhQTElFQkA+PT4/QkdNVmX35t3Z2t3j821eVE1IQz89Ozs6Oz0/REpUZuvXzcbBvry6uLe3t7e4ury+wsfN1N3sd2ZdWVVSUE5OTUxMTU1OTlBSU1VWV1dXV1ZVVFNSUVBPT09QUVNVWVxia3zv5N3Y08/NzMrJyMjHx8jIycrLzc7Q09bZ3eHn7vt1bGZhXlxaWFdWVVRUU1NTU1RUVVVWV1dYWVpbXF1eYGJkZ2psb3V7/Pbw7Onl4uDe3dzb2tnZ2NjY2NjZ2tvc3d7g4uXo6u3w9Pn9fXp2c3Bvbm1sa2ppaWhoZ2dnZmZmZmdnZ2doaWlqa2xtbm9wc3V3eXx+/fv5+Pb08/Lx8fDw8PDw8PDw8PHx8vP09fb39/j5+fr7/Pz9/v7/fn59fX18fHx7e3t6enl5eHh3dnZ2dnV1dXZ2d3d3d3h5eXp7fH19fv/+/f39/f39/fz8/Pz8/Pz7+/v6+fn5+fn4+Pj39vb29vb29/f4+fr7/P39/v5+fXx8fHt7e3t8fHt8fX5+//79/n5+fn58e3p6e3t7e319e3t8fX18ent7fH15dn3+/fX1+X19/P55e3787/n57ut7ae5yceX3bulv/mVs3Xt82+726uV78PdT7OZ7YVlo+29n/u1dcujQ4dflUGDtWFx75t3kWs7M597dfN9gW3lhUk5NTN9kV8vHa93dd9XmWfTfaVZPVmlUR1R6/fnqzb7C69rM4ExOS1pPRkbu6G53+tbL52DT0+Z659bM1+7UxMnQ1crJ8EtHRD89Oz5Y2NPKu7e/0GRLQTctLTIyMj5U7Mu+trK1vL6+w8nIxL62tLSwsLa8xdPxWUxKSklWXltSTUc8My83NzU+eMjGysnH9DsvLSwmIyYtMTZF48vHwL28wt/Zw8bDuLmzqa22r628zOP+0ORKVMnH13Hu3G0/OT49NzlJ7b+8vri3yVA9NTEuLS4zOUJbZV9s19xpaNzHw8K9sa2vr7K1trjIzcrKz97t2M3calxieFpFQERIQEVANz1NZt3Kyca/1ks6Mi8yMjA0OUBNTkZFT2VkWfPGvLe3t7Gur7K3vLq7wMnHys/a+HFmZ1lQVFJNRkhJRkRLPjdAUePLx8zHw9tNNy4vNjk3NjU8S1BHQ0hd29vWx7y1srS1s7CvsLe7vLy8wM7f6/55alRPUVNPSkNESUZJTj48QU7axcPHyNbpUzkvLjE5Pjw3NjpEUVVPUF7dxry4trSzsa+vr7K2uru7vsLN3fV1cWxbTUpJSktNSUhFRE1FREJEX9XFwcTXZkk7NjExNDg7Ozk3OkBPbfb6furMvLOurrG0s6+trrG6w8vLycnN4WFQTU9UVE5IQ0RHTE5RTERAP0pp0cTDyuRZRDs1MTEyNTk7PD0+Q05g59rW0s3DubGtrK2usbS2t7e6vsfV5/1ybmtfVU1HRURGSUtMTEpJSUZKTV3n08zN1ftQPjUvLjA0OT5AQD9BR1X91svIxsO+t7GtrKytsLS4vL7ByM7a7WtcV1NPTElGRERGSEpMTE1LSUpNXPHTysnM3Fs/NS8tLjI5PkVISElNWHvYy8O/vbm1sa+ura6usLS4vcXS7WJXUVFRT0xHQj8/QERITU9PT0xLS1Np383IxszeUz00Li0tMDY8Q0lNUFlo69fMxb+7trKvrq2trq+wtLi9x9lqUktJSktMSUVAPj5ARUtRVldVUE5QW/rWy8bFyt1UPzUvLS0vNDpASU5TWWft2c7Iwby4sq6trK2tr7G1uLzDz+1cTUhHSEpLSUVAPj4/Q0tWX2djW1RQVW3Zy8XGy99RPjYwLy8wNDk+Rk1TWmTz287Iwr66tbGuraytrrK2u77FzNp9W05LSUpMTUtHQz8+P0RMWm76e2RYT05Z/dXMy8/sVkQ7NzU0NDU2OT1FUF9959zSzsnFv7u3sa6tra6xtru/xMnM1etmVExJSU1TVlFLRD8+P0dSb+fk8mNTTUxVcN7X2elgTkZDQj88ODUzNDhAUXTg4ujg2MzFwL+9urawrq2usri/yMvLycrP42VTTU1PVVhUTUhEQkRHS1JaZGppYFhQTU1RXGl2fW5gU01JRT86NzQ0OD1JYO3n7ejVyMC/wMTFvbWurKyvtr3BwMHCxs7fcmBaVlBNTEtNTk5LR0VFSE1TWl9eWVdVU1VWV1lcYW12ZFNIQT48Ojg4NzpBTG3e3+fdz8W/w8jKxbmvrKyvtbq7vL7G1eHb1NXnW0pHSU5WV1BLR0dJTU9QTk9XaW5dT0pKUFdbW1dXXWhjV0tBOzk4ODw8Pkpd6dbS39TIxMTJzMi9tbCvsLGztLnAzNXSztDe/mpnaVlLR0pQV1VLRUVJT1NPTFBeYFhQTUxPUk9QVlpbXV9dW1BGPjs5Ojw6PUpe7dbZ7M2+wMvMycS+vLmzrq+ws7i7v8na39rd4vBzZmFWTU5PT09NSkpMSkpLTlVbXFdYW1VNSkxOUE9SWmNuZ11RSkA8Ojo6OT5LW3jZ2dnBvcjUxMDKycG5srCxsK+zvMPI0eN9ZnLtb1peZltXV1hUTUhJSkRDSFBTU1tfYl1UTUxLSEhMT1BZbvBtV05JQDo5OTk8Q1By3tDQwbnDzr+90NO/u7u4sq+vs7i6vs3t7fRhWV9vZVxgaWRcV1JNSUdHRkVHTlVSVWJsWk9PUkxGSExLSVFZVlhdT0ZFQzw5PkBETm3Zz8u9uL6/ubzKxr7AxLq0tba0tLnAx8veY1xbV09RWGlmYW1vWU9OSEREQ0NJS0xVW1tdXlhSUE1KSUlLTU9QUlJMS0ZCPj5CRUZP5djRwLq7uri4ubu7ubq8ubm6vLu9wMbKz993YlZLSElIRkhLTExMTk9MS01NS0pNTEtKTU1LTVBPT1NRUE9JSUU+PkA9P0lMXd/Pxby7uLa2trW3uLe4uLe5ubm8vr/HztPnYlpPSEVEQkJDQ0VGR0lKSktLSElHRUNDQUNEREZKSUpNTElLSkZGRUZHSk9k6dbFvbu2srOwsLGys7W3uLq7vb/Bw8nNztrx/mhVUk1JSERBREI+QUI/P0NAQEA/Pz89Pj88PT9AP0RHR0xPT1lcXnb35d7NzcXBvLy4t7W0tbW1t7m5vL2/wcPHysvQ1tzfbm1fUk1MRURFPz8/QD0+Pzw+QDs/PEI6RjxFRkFKS1pKbVjnYt3p0trO0sbMyMbCxsLFvcTDwsDCycHGxcrGzcfSzdjN3+Pa9/JqfFxoUV5WT1NMWURYR09HTUlOSkhOU0ZOU0xTT1JaW1NfaWpZ41jTVdZo0Wnd193r1drT4dPczNr0wvnQ2dHfzv/N4Nzo2OHo6N9q02H63WR9cOBP2VHoX2lb7lxW+mNZZmpXfFhmXW9a+VL7XXNbcmNtYnV1Xedd4VnVVdlf3WHcaONw7vTgY9n0+ONt1FzQWc9l4Xre9nLdaNpi3GPZY+Zt6XLsYuV9fn1x5l7fW99b3VroZ/9ufXB9a3T5X+duc2XiYv1g21joXdxc9GPbXOpq6Xto6G7kWtdh517ZXuZk4Hdw6nvzeXHod/l342n06WzhaOZs32fu7XL/9vN19+546m/pdu5k43Zx8vv9e/hx6W/8cOhm73bnaelq5G11/PJwaOJg6Hli2mP6eexq9PvzY+xrZN1Z6HfvX+H9Y9tedt5fad5e9HT5XdhfYNFiZtNh7X7fWtRhb+DxV9/kYnjp9exp3vTdYO7cb2Fr11tl9N5f+v7dcv7+6OhP0GT0Xttf32Br3PlV6t1ZfPBk721d4e1XddxoXe9wfF5h6HVZfuxzfmTr3Fd03WJW5XRh723042hs02xm6ulqdWT57V1i2nxm6dzh9X3c41xy7G5mZWblcl/t3Gr36Xx8e2V8fVh45mtq3/Pt63Ru3WFbd+xcbftz9+h86tx0+N/1Yf55fGB46uZv+ejd9Gzw3et27+Di9Hfr3O135tn9cPfpem934O5q+9/o93vo5O5w9+n4aHzqfHv1+/XuZfvda1zi9mF88XPpbnfg9Vrw42Zv8PD9fnvyd3f19HBt+vNscXvr6mti4+1aXWxt8mtZfupcWvpsXl9dY35hXG1qXmhpde73e/D7b/T7e+rrfOffcWR7/GhfZP91YGTvfGxy7PxrXVdPTElFTGnWysTMzMzzTEI5NTk5PE31fNvLycrCw8vGxcvMyMfDwsvU0NtiVl5eXlxdXGJcWWRmXXTgcVxrW0BASD9AXtrFv8/Wze9EOzc4PUNWzr+/v76+wcnOz83Hv7y6trK2v8rP3m1ZYuPddmRrX1JOT1BOS0pRVk5JSlFbT0JTXz89S0hHWOnKz11UbVhANjZFVlTuyMPHz+fcyMrUy8C8ur7AurrG0dfq9+v13svQe1dOS0VBSmN0bXJlWVBDP0RJUlhVb3FNREdKS09m2NLqYV5tZjw0RGhe89DIvsn04crL0c7Cubm+wMHEydDf79vS09PU09L1TEZHREpYZO7hcFZMRUFESE9ofGFeY1RBOz9TfPzlzMngVUhFXOc9Nn6/zd3t683Q+fHHvsLKxsPGw8jJwr7G2PT24uHn6c3F205FR0tYYE5Od35SSkZEUFpNTmV2ZF1PRkVNV1pr5Olve3xqcd7rVlZxzslUP1bw3c7Q0se/yd3b0tPJxcvEurvFzd57bFlSfMrEys3YYExFPj9a6W5WTEtRTEA+RVzg71lfZ1RNRkBM8Ohy6e1dXGdcYezzet7Qz99wa25t/svA3FJtz8jJ3WrcwsDJ0NbPxMTT9OTJwcXZWFFdXlNUctXGxOBJPT1ASU9LSlJbV0k+PUBLXmBaZvDpbU9FRFXk2uHwbWfz4OPg3ODi19Tc5fD55dnYzcPQZWrdzsfJ297LxMbJ0eDNxcza3tvUy9NoVE5PVl5v5dTN1GBEPDs/SElGRUhRWE9FPj1DUF5fYmtxcWdXT1l56e5nWWXbycTL33vv5OHefWzazcnFxdH279vPx8jNzcvJxsTK0tPQz87N1Obq/F9YU09VedjNzNtdRz08PT4/Pz9BR05TUk1IQ0NHTl1793ltbH7s9GBUUl/cyMLDytfr+X767+/r4NHJx8jO1djX0M3Ly8zNzMrJyMrO1dXZ3t5+WFBNT2rUycXG2ldFPDo7PT4+Pz9FTVVeXlJKRkVIUFxq/e/s497h6fh3+eLWzsvN093sfXz27eXd2tnW0s3Jx8jKzdDR0M/Ozs/Q0NDOzs/W8lxOSkxc58/IytZ1TkE9Ozo6Ojo9QUlXce7wcVpOSkdITVdo6trV0tHS0M/Q09jh8Xt1/Onf2tfV1dTV08/NzMzMzc3Nzs3OztDQ0tPT1NfffF1ST1dt3c/M0elYRj46OTo7PD1AREtVZH72dF5SS0hKT1z63tbS0tPS0M/O0dnneWdncfDf2NPR0tPT0c7NzMzOz9DR0M/Ozs7P0tTY3utpWFFSXPbXzs3XcE9CPDk5Ojs9P0JGS1NhdHluXE9KSEpTaefY09TW1tTPzc3P2e1rY2j639XPzs/Q0c/MysrLzdDS0s/OzMzN0NPX2Nrg919UT1Ni6NPP1O1YRz47Ojs7PD0+QEVLU19nZFtQS0lLT1195t7c3NvW0s/O0Njj9nr249nRzs7Pz8/NysjHyMvNz87NzMvMztHV19nb3ul6YVlVV11peHxwYVdPS0dEQT8+Pj9BRkpMTU1OT1BTVVZXWV1ld+7l397d29jW09LS09PS0M7Ozs7OzczLysnKzM3Ozs7Nzs/S1dfY2dzh7XlqZGBeW1hVU1NTVFRST01MS0tLS0tKSktMTk9RUlNVWFxfZGhrcH7u5d/d3Nva19XT0tLS0tDPzs7P0NHS0tPU19nc3d3d3uHm6+7y9v14b2poZmRiX15dXFxcW1pZWFhZWVpaWlpbXF5eX19gYmRmaGlqa21wd3z+/Pz69vHu7u3t7Oro5uXl5eXk4+Li4+Tk5OPj4+Tl5ubm5+nr7O3t7u/w8/X19fb3+vz+fn59e3l3dnZ2dnV0cnJyc3JxcG9wcHFxcXBxcnR1dXR0dHZ3d3d2d3h5e3t6eXl6e3x8e3t8fX7//35+fn7+/v9+fn7+/v7+/37+/v39/v7+/f39/f7///79/f3+/v79/f3+/v/+/v39/v7+/v7+/f39/f7+/v7+/v7+/////////////////35+fn59fX19fX1+fX5+fn59fX5+fn59fn5+fn7/fn59fX5+fn19fX19fX19fX19fX18fHx9fHx8fX18fH19fXx8fX19fX18fHx8fHt7fHx7e3t6eHd4eXh5eXp6eXp+/f5+//78+vj2+P1+fXx7fP/9/fz8/3l1dXBub3j+/Pr08fZ+b258+H549Obi6PV4fPDo6HhiZ/1vW2Hm23xw3M/jWkw+67rPQUL2yeZEUMvLYFLczd1rceh7TU141t91beDa3WtZXXRsYVto2dDb7O3n6Whfa+3menDp3O5oauvdeWXw6m1w8tnS3u3a3epwXlX75G5eaG9pYFRPV1xRTlRfcG9VUnzkXFzf2/Dr39rV4uXU1H125tvi3tfQ2+Xe0tns6t3m3eP+/uTs7Glbbt3tZGJoa2NYT195YE5WbPldVV99XVBXbHRlYGfr8V5UavlrZGtrffBpcfP99+t7cujj8O7k5Onn/fre4Hno2e9v3Nvm4/B83PJX6tDja+fi8PN9b9/lbHvf5O96fdzqeujudfNzae3ibvTc91tn325ea97vYu3obFn6Z1/12nxybl1qYWz+11535PBOT+1+62dy/dxXUXHpYFrn6+ZuYV/n/Vtk4t5eXWTb5lZk2d9WW+fW+Fhe3dJdUOTWZWbp8el+X27ie3Dx/m/m7Wvr8WRv4vzu6nFy4/Vp6+92fev8//xkaub7Zejkfvz3YnLj6W3n3fdiXOvi+Ghv/NviVmzV3lti5d99X3Tj6mh92+RsaP76/HN16eJ4Xm7vd2b24OHre3fyfGRt5+RzePr+/2xe79lxXG7neWxp9N3paGrn8mpv6un3d3n2cGt0b2b/82phfOb+dfjqfH7q7e72d2ru6fbw4+V+fPfrdGVn7+f7e+71ZmFdXWj15unn6vtkWVRRTktMUFdZXWVy8d/Sz9TY0M/QzsrHxcfLys3k/N3OzN377W5HPkRMUFtqffViRz9CPzs6Ozw9PT5LXmdp5dHOzsvFwcDAvru5u7u6u7/Eyc/Y4/p3bmBeYl9bXlpPV/Dh8ujZ3mRRUVldXWJ0bVNGQT87NzY2NjY3O0JLVGvf083Jw769vbu5ubq5ubq7vsLJ0N3ud2hgXl1cXFxdXlte/eDj3tbX9F5ZWlpbXmNeUUdBPjw5NzY1NTU5PkhSaObWzsjCvbu6ubm4uLm4ubm8v8bN2OtuYFpUUVFVWFlZWVtlfOve19fid2JcWVtfYFpQSkM/PTs5NjQzNDY7Qk1e9dvPycG9u7m4uLi4t7e3uLq9wsnT4P1mW1VSUlZaXV5eXmNv8uDa2t/xaVxaXF9eWE5IQj89PDo4NTMzNTk+R1Jm7NfLwr26uLe3t7e2tra4ury/xMrS4XNdVlJSVVpdYGFiZWt0++3o6vJ5aFxWUE1LSUdEQD49Ozo6Ojk5Ojs+Q0xafdzPx8C8uri3t7e4uLi5uru9v8PIzdbjemNbV1haXV9jZWdoZ2ZiXl1bWVhWUU1JRkRDQkJCQD8+Pj4/QEJDRkhMVGH84trTzcnEv728u7q6urq6ury+wcTHycvO0tnj8Px4cW5oYV5bWFZWVFFPTUxLSUdGREJBQD9AQEFDREZISkxOUFVZXmd47eHZ087MycfFw8HAwL+/v8DAwcLExsjKzM/T2Nzi6/Z4bGRfW1lXVVRSUE9OTU1MS0tKSkpKSkpKS0xNTk9QU1ZZXF5kanB+8erk39za2NbU0tDQz87Ozs7Ozs7Ozs/Q09XX2t3f4ubo6+71fndyamVlZGBfYGFfXVxbWllYWFlZWFhZVlJSVFRUVVhaX2RhZ3p+dfrr6evo6ebf39/Z1dja1dPa2dff6OLh6N3T2ern3tXcQni4y0FPz+JTddDdXGnd4mRgX1xaWV1bU1NlZ1FKUFxNRVb0eGt22ttiU2zxVUbmxHtGdM3uT1DZxPZO5sjgXt3H2GrezNPq3NLX5tzR3Hvu19T8Y97UaVLm02JX3NlwaPLq5m9YedtkTmv2XFZnbXpsVlZ3Z09a935aYHhzX1xs/3JcbeJ4Wn3db1zy3GxXZ+n7ePLq7vxjfd3rZvHf5v768OrpeHPf4Hvz6+rl/nDk6Gz95Xlq/un47v704/1b/uFmaN3mY2vu9Xtvee53YXXtc2pv9/xwZvnqbWD74+twbefkZ1313/tk8tf7WvHacGfk6Gdv9nrz4/F26/BtbPFwdPP1cPXza2n26v1+e+5sdfdpZtrnVHnY/1rt7/nvdFns2l1U2dhZX93jeGhv63xeXu/1anTd6GZs5f5aZ+Lhcm/o5mxncOvuenTxdGZq+/F+efHldmR97358a23v8HD45/F1c/h8c3z07/lwd+z8b/Xl/Gtvem9xcm3i2Xxh7eh0WV7k22Nq2OFdZmlo6vB439trbftrZnh68OX2dvLwbWnu8X7r7Xfo6Grt3fly8XRrfHp16uppavJyYGx9fndiX3B2aWlxdXBvb/vl5vLp2d5jWn3o/f7i2trg9mxnZm7m2eT7dGBTU1xiWldeX1dXYmncyMbGxtLsb1JLWGNg8NPNzdPibFlUW/XXzMTByNLb62BRVG3j3uV+XUxAPT1AQUVQWE9MQkHYy1hY19LhXVrSyOpo1MjJ1d/Mw8zQxsXM2HNdbGFYd+Pg0czR0+BaVGFk9c/NzMnaX19ZSElYXmhwXU9IPzw9P0FHTlJQTUY/PujIZl7R1+X/WNvH3PHRy8bM5M6+x9LGv8TL3O3d5WJ91tPU0M7M0m5ZaP965tfOyc/8aHZcS0tabF9UUE9HPjw+QEJFSUxPTEVBPmbI+mDU1+flXOzHzuDSzMrN4dbDxc7MxsXK3PDe33506NrW2NrRztp+bH3v7e3d1dXc6vptWk5MTlNUVE9KRUE/QEFBR0pHRktMSERF1ML/+8vR2+tY3cXO2cvKzNjr0sPJ0c/MzNLe2tXf7frz3Njf2tLR1t3i4etxbXn18/v7+HZhWVJPT05MTU5NTEpHSUpGSEtJS05LSklMW1J5ydHg1tvZ1mf8ycfNzdHLyNnj0dDQ1drPzdrp497d7fzm3NvZ1c/O1uf17e7w+/fr6n5vcmxdUlBRUVVYV1pcWVZYWllUUE9Yak5EUFhLSUtUXk5HU1dMS01j2Nrl1s7N0NzTyMfJycjGyM3PzczNz9HT1tzj5ePn7/ru6vDx8O7m3t7e3+Hg5Orr+XZ0Y1lYV1dXUFFZVVBSU1ZWT1FXVFhbWWFpZm5oXFxYVFhWVFpcXF9dXmBeXV9gZGhpduzf3d/g29fV1NPPzc3Q09TS0dHQ0M/OztDS09XY3unw+3ZxcHBubG1wcXJ1fvLr5+Th397e3+Xs93VnXlpXVFFPTk5OTU1NTU1NTlBUWF1lb/zu6ebm5+nr7e/w8PDv7/H09/n6/P5+ff/69O/s6ujm5eTj4uHh4ePl5ujp7O7v8fT29/j4+Pj6/P39/f39/fz7+/v7/P3+fn17enh3dnV0c3NycnFxcXFxcXJzc3R1dnd4eXp7e3x8fHx8fHx8fX1+fv/+/v39/f38/Pz8+/r6+vr6+vr6+vv7+/z8/f39/f3+/v7+/v7///9+fn5+fn19fX18fHx8e3t7e3t6enp6enp6enp6enp6enp6enp6enp6enp6e3t7e3t7fHx8fX19fX5+fn5+fn5+///////+/v7+/v7+/v39/f38/Pz8+/v7+vr6+vr6+vr6+vr6+vr6+vr6+/v7+/v7+/v7+/z8/Pz8/Pz8/f39/f39/f3+/v7+/v7+/v7///9+fn5+fn5+fX19fX19fHx8fHx8fHx8fHx8fHx8fHx8fHx8fH19fX19fX19fX19fn5+fn5+fn5+fn5+fn5+fn19fX19fX19fX19fHx8fHx8fHx8fHx8fHx8fHx8fH19fX19fX5+fn5+fv///////v7///7+//7//v/+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v79/f39/f39/f39/Pz8/Pz8/Pz8/Pz8/Pv8/Pz8/Pz8/Pz8/Pz8/Pz9/f39/f7+/v7+/v7//35+fX19fX19fHt+fHt7e3x7e3t7e3t7e3t8fHx8fHx9fX19fX1+fn5+///+/v7+/v39/f39/f39/fz8/Pz8/Pz8/Pz8/Pz8/Pz8/f39/f39/f7+/v7+/v////9+fn5+fn19fX19fX19fHx8fHx8fHx8fHx8fHx8fHx8fHx8fHx8fHx8fHx8fH19fX19fX19fn5+fn5+fn7///////7+/v7+/v7+/v7+/v39/f39/f39/f39/f39/f39/f39/v7+/v7+/f5+//3/fv7+/v////7//////v7//v////7////////+/v7+/////v7///////7///////////7//v7//v////////9+////////////////////////fn7/////////fn7//////35+fv9+fv////////////9+fn5+/37/////fv////////9+fv////9+fn5+fv///37/fn5+fn5+////" 
            : "UklGRrRUAABXQVZFZm10IBIAAAAHAAEAQB8AAEAfAAABAAgAAABmYWN0BAAAAGBUAABMSVNUGgAAAElORk9JU0ZUDQAAAExhdmY2MS4xLjEwMAAAZGF0YWBUAAD9/317enx+/fv7/X16eHl6fH19fXx8fn7/fXp3c3N1eX3+/316enz++PPw8vT08vDy9/55dnRycHB0++vk4ul7ZV1bYnvn3d3p/nBqb25hX2f42tPR1N7tZlZRU2Li1NxcPzYzOEB7zszJ1+Pe4OFpTz45Pl+9ramsvU4wKy491b20r6yuudk4MTNOsaWforF7OS8uOj5U+kkzISMmSKGgoLUtICEpScVWOzU0u6SenrkvIB0mULesq77Z5mnU2UA0LS/ltKKcoKm/TjE557ahn6KyQiQdHCZ0s6er/DYrLkDg7Es3LzlVyL2/czs0Lznmuq2uv04vKSowP+fP3c69ta2rq7feTkncvLOrqa2ut7fVOiwZHiU/pZ+fqkMkHyIt+NjW7Dg/T9y5w0ItJyk/vaurwzomISU21Li7v9G/rKmmqrLKWEvcu7KtpammpUgqGhQfPK6en65sMSkwOz1FOTdATMy/x1QxKSs05Ly2xkMvJicsOGTYvrOsq6muvbvCwLi7t7Oxr6usqatCJxsZK8ajnKTKNycpOk3gaz8zNDlgy9VMLyowPcm2wlwvJCIoNmi9t7Kuraqrr7XEycG8sayrrKusrq3sJx4aI9ClnJ+/MyQiMF3IwV41LC45T/ddPzQ3QmLH4EcvJCMpN229s6yrq6qut7zHyb2zrqusra2vtrs8Ih8hNrGioaxKKSYoQcG9xFAsKCowSHteU0BBSlzyTTYoISMtQMmuqaOmq7LAxsvHurGvraysra+z0kAeHyhBqZ+jsj4lJi9Ps7jJQCciJi1WwsPPVz8+P0g7MCghJSxFvamjn6OsuOls3ca0rausq62yssBHKRsjPradobJMJyIwUbqtxEctHyEqO8W2x+JCNz1AQTYpIiErP8apoJ+hqrfI+9jLvrCtq6qqrbHLOSofLGGuoKjDNycmO820r8w3KB8iMle4tdJcODE6PjgwJSEkL1a5pp2epKzBeOniwrCurKquqrO7Xy8oIjvMqKKt1TAmLk/Isr5CLiAjLEW+u8lOODM+S0M6JyAgKDzUr52bnqO66V9cxrOwsK2xra65xzouKDPmtamxyEEtMT7uvctFMiIkLUW7t71fMi0yO0g8LiYiIixFvJ2Zmp22Vz5A2rKtq6uxsLbA1kAyLDBGy7Gvsc9HOjQ6S0M+Ni0wOWK8ucVZMSstNkNGOC0oJi5XsJ2Zm6S8TUBK07SyrrC0sri/4EY0MDdFzry4u9tNQTs6Pjw8OzY7QFnQyc5wQDc2Njk3MS0rLTvmqpyZm6fDQDxIy7W1sLW3tLzJWzovMTpVxsHB0lROSEVCOzUzNTtO/M3P5mBJQT07NjMuKywuOVy9o5qana7pP0F0v7O5t7u6srvHUzYvMD17xMXUVURGSVFHOzQwNEBl0sjcXUtBSEpKQDUtKCoxPti0opmaoKxgPk5WuLe6vMi8tbK8bDkqLDZZwMzXQjpETeBfPzMqLj7qvLzSSjs5R3ZwTjYrJigwQM+xopiZn6dZS09Vtrq6w8a5s6284jkmKzBbv8PhPzQ7Sv1uQi8oKzfft7O/SzcvOWzV3D4sJCQtPtCxp5iZnqPlW0/xtLW50djFtqqwvUEoKC1XwL3TOS8vQM7P4TQmJypNuLGzXzYuL0vX20gsIh8pOdqyqpqanqDDbVlVvLW5v8bHs6utt00qJypLx7W9SDctO2jX0zspJic/w7Gvzz4uLDlV4lUwJB4kL264rJubnaC720tuxbG2w8HKtKmqrugwJyc91bO22z0tM1HfykosJyQy8reuv1AzLDJG+lg5Jx8hJ0DEt5yam560y0day7iwwL3At6isrco4Kyg0W7y2yE0vLzxgy+88KiQoO8uvsMZFLi84UPlLLyEfISxh1Kmcm5mjtMVOz8+5vcu7xq6srq/zOi0pNlrBvdY/MjE7b+dvOywnKj7WsrTIVDIyOEd0RjMlHycrSr+rmJibobjlYPi/tr7Ey8G0rKm0xzorKyxI5NjdOTQyOW7gzEovKyg247etuvw4LjNAalo6KSMlKUDaqJiZmJ+wuXnLwcbGcdfJuKuurb5FNSouOUNnRzsxLzlK0dtXOCoqMF64rrHMQzM3RmR2Pi0pJCkvRKufmZiepLXDwcW8ztp5eMW6rq20yUc4LzM6Oj80MC8uPUhj5UU8MDFCd7u5wdtJRkpcbEU9MC0tJzNss6CenaGrr7e0tLm7zM/f48vHwM/XZEtIPkE8NzUuMDI2Pz5DPzc7PEzfzsXcbVxb4epwWk1SPTY3PFvHubGxtbi4srCurrCyt7u7vsTL1eZnWlRKQDw5ODc2NDI1MzIzMjU5QUlOT1Bj49HL2NbK0O5WUE1c287Iys/JxLu2sa+urq+vr7K0t7vAyc/+VUhAPDg0MC8wLSwrKy0vNjw/REdSbePM0M7HyNxqWE9X69/V2/7l2sq+trKvrq+vrq6tra+zt7zGzedlVkY6Mi8uLiwoKCYoKy4xNDk7RVnp1MjGw7/E1ef47trM0d94Wmfgy7+6ube1tLKurq6trrCzur/CxtDuUj42MjMzLCcmJigrLy4vNThCVW3ayMTCwcfW0c7Kw8bM2eHt1cfBvbq7ure2tbCvsbGzt7i7wMHH0ftXPjc0MS4pJCUnKCwuLi02PEdq6NbKxsjHys3IwMXEz9zd19LPx8rGwb68uLe2srKzsrK1tre7v8LT7VRCOzczLCglJSkqKywsLDA7Qlrj08zKy8rHwr+7v8PJzs/OysbEyc3KxsK8uLi6ubm2tra5ubm/wczlYFRDOzkyKSYmJykqKykrLDM9Q1X02tfMzMnCvby5vMHHx8TCv8HAx8zMycS+uru7vb26t7e3uLq9wMjO3mJOQDs2LiclKCgqKiooKy83RE9f4dnTycjEvru6urzCwb++vby/wMjLysW/vbu+v8G/vLm6ur2/wcjL2O9RSD45MyslJikoKSkpKC0yO0xX+9rPzsTAvbq2ubq9wb28vL2+xcnLzcrDwcDBxsbCvbu5vL2/v7/DyNL0T0g/PDErJScoJygoKCcuNDtKV3HezsvDv7y4tru7vLy6ubq8vcPFycvLxcbJys/Mxr+/vr/Cwb/BwsnS9VRKR0AxKykoKCkpJygpLDI5QlRo6tLKxb64t7W3ubm3t7W1ubu/wsjIycnK0dLTz8rFxMPDxsXFxcfL1XVPS0xCMSwqKCgpKSYoKSswOEBOaeDPx8K7trSytbe3tbWzs7e7vsLExsjLztfc1s7KyMbHyMjEw8THzNt5V05VSjYuKykoKysnKCkqLjc9Rlp+28rEvbi2tLW2trS1tbS2ur7AxMbJzM7Y3dvRy8rIzMrLx8PEyM3V9FxVU009Mi4rKissKikqKy01PUVRfd3Ow725t7e2trW2tri4uLu/x8fHys3P2ODd1M7IyczPzMvGxM3W3vNdUE1KQDgwLisrLCwrKywuNDxFTWXhz8m/vLm5uLe3tba3urq7vcDJy8vP1NTd7+nd19DR1tfTzcnIztHV4XteU0xEOjMwLi0tLiwtLzE4P0lRbN7Pxr++vLu6ubi4uLq7vL7AxcrNztPX2OH16eDb1dXb29fRysnN1dvnfWJTS0Q7NTIwLzAxMDA0OD5IUVp13M7GwL6+vby7urq8vb7Aw8bKztbc3eTq7fZ+8ePd2tjZ2dXR0dTc7XNgVExGQDw5NTQ1NTY4Ojs+SFFe+eHYzcfDwMDAwL+/wMLFx8nLzdPa4ent6+jr7/H07eXg3t3f4N7d3+LsdmRbU0xHQz8+PTw8PD0/QkdMU15x6dzVzsvKycnJyMfIyszO0NDS1tre5eXk5Obr9PDs6ujm6+3r6+vq7fl8c2xpY1tVUU5NTEtJSEhISktNT1JXXWd28OXf2dXRz87Ozs7Oz9DS1tja3eLn7fb8fHRvbGttbnBzeHz8+vf08/b5fHFsaGNfXFlXV1dXWFlZW15hZWlscHd++fHu7evp6Obk5OTl5ebm5+jq7O3u7/H0+Pr7+/v8/v7+/v39/f7/fn59e3h1dHJwb25tbGxsbW1tbm9xdHd6fH1+/v39/f3+fn59fX19fX7//v37+vn5+fn5+vr7/f99e3l4d3V0c3N0dXV2d3l7ff/+/fz6+fn5+vv8/f5+e3l3dXNxcG9vb3BwcnR2eXx+/fv5+Pf29vb3+Pn6/Pz+fn58e3t7e3t8fH1+fv79/Pz7+/v6+/v8/P3+/359fHp5eHd3dnZ2dnZ3eHl7fH1+/v38+/r6+fn5+fn6+/v7/f3+/v9+fn5+fn5+fn5+fn5+fn5+fn19fXx8fHx8fHt8fHx9fX7//v38+/r5+Pj39/f29vb39/j5+vr7/Pz+/v9+fn5+fn5+//7+/f39/f39/f39/v7+fn59fXx8e3t7e3t7fHx9fX7+/f38+/v6+vn5+fn6+vr7/Pz9/f7/fn59fX19fX19fX19fn7//v/+/v7+/v7+/v7//35+fn59fn1+fn7//v7+/f38/Pz7/Pv7+/v7+/v7/Pz9/f3+//9+fn19fX19fX19fn5+fn5+fv//fn7/fn5+fn19fX19fX19fX1+fn5+/37///7+//////9+fn5+fn1+fn1+fn5+fn5+fn5+fX19fX19fX19fHx8fHx8fHx9fX19fX1+fn5+fn19fX19fH18fHx7e3t7ent7e3t7fHx9fX5+fv////7+/v/+/v7/fn5+fn59fX19fX19fX19fX19fX1+fn5+fn5+fn5+fn5+fX19fX19fX19fX19fX19fn5+fn5+fn7/fn7/fv9+fn5+////fv///35+/35+fn5+////////fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fv///v7+/v7+/v7+/v7+/v7+/////37//35+fn5+fv9+fn7///7///7+/v////////9+fn5+fn5+fn5+fn5+fn7////+/v7+/v79/v7+/v7+/v7+/v7+/v//fn5+///////+/v79/P3+/f79/f7+/v7+/v7+fn7///7//////v39/v79/f39/f39/f39/v7+/////35+fn5+fn5+fn5+/////37/////fn59fX19fX19fX19fX19fX19fn1+fn5+fn7/fn5+fn5+fn5+fn19fX19fX19fX1+fX19fX19fX19fX19fX19fX19fX19fX19fX1+fn5+fn5+fn5+fn5+fn5+fn1+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+/////////////////////v7+//7//v7+/v7+///+////////////fn5+fn5+fn5+fn7///9+fn7//////v7+/v7+/v7+/v79/v7+/v7+/f7+/v7+/f39/f7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v///v7+////////fv9+fn5+fn5+fn5+fn5+fn5+fv9+fn5+fn5+fn5+fn5+fn5+fn5+/35+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fv9+fn5+fv///35+/////37///////9+fv////////////7+/v7+///+//7//v7///7+/v7+/v7+//7+/v7+/v/+/v7////+/v7+/v7//v7///////////////9+////////fv9+//9+//////9+//////////9+/////35+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn7/fv//fv//fv//////fn7/fv///35+fv9+fn5+fn5+fn5+fv9+/35+/37////////////+/////////37///9+fn5+fn5+fv9+/35+/35+fv9+//9+//9+//9+fv//fv9+/35+//9+fn5+/35+/35+fn5+fn7/fn5+fn5+fn5+/35+/37/fn7///9+fn5+fv9+fn5+fn5+fv//fn7/fv//fn5+/35+fn5+fv9+/////////////35+fn7///////////////////9+/////////////////37////+////////fn7///////////////////////9+fv///////35+////fv////9+/////////////35+//9+fv9+fv////9+/////////////35+/35+fn7//////////37/////fv9+fn5+fv9+/37/fv9+//9+////fn7//37///9+//9+/37///9+fn5+/35+fv///35+/35+//9+//////////9+fv9+fv////////9+fn7//37///////////////////9+fv///35+////////fv////9+//////////////////9+fn5+fn5+////fn5+//////////7//////////37/fn5+fn5+//////////9+//////9+fn5+fn5+fn5+fn7/////////////fv9+/////37/fn5+fv////////////////9+/35+/35+fn7/fv///////////////37///////9+//9+////fn5+/////37//37//////////35+fn5+fn5+fn5+fv//////////fv//fn7/fn7/fn5+fn5+fv///////////37///9+fn5+/37//37////+//9+/////35+fn5+//9+fn7///////7+/////////37///9+/////////////////////37/fv///v///////////////////////35+/35+//9+fn7///////////////////9+/37//37/fn7///9+fn5+fn5+fn5+fn5+fn5+fv////////7//v7///////9+fn5+fv9+/35+/////37///9+fv///37/fn5+fv9+/////////////////////////35+fn5+fv//fn7///////9+fn7/////fn5+fn5+fv9+/////////35+fn5+fn5+fX1+fn5+fv///v7+/v7+/v7+////fn5+fX19fX19fX19fn5+fv///v////7+//7/////fv//////////////////fn5+fn5+fn5+fn5+/////////v7//////35+fn5+fn5+fv///v7+/v39/v3+/v7/fn59fXx8e3t7e3t7fH19fv/+/v39/Pv7+/v7+/v7+/z8/P3+/n59fXx7enp5eXl5eXl5enp7e3x9fv/+/f39/Pz8/f39/f7+/v7+/v7+/v39/fz7+/r6+fj39/f39/j5+fv7/f5+fXx7enl4eHd3d3Z2dnZ2dnZ2dnd3eHh5ent8ff/+/f38/Pv7/Pz8/f39/f7+/v79/Pv6+Pf19PLw7+7u7e3s7Ozt7e7v8fX6/np0b2xpZmNgXl1cW1taW1tcXmFlam95+/Lt6ufl5OTk5ebo6evt7/P4/Xt2cW5sa2ppaWprbG5yeH769O7r6OTh3tza2NbU0tHPz8/Pz9DS1dfa3uPq831vaGFdWlhVU1BPTk1NTU1NTk5PUVJUVlhZWlxdXmBjZ2x1/fDo4d7c29rb3N7i6e72fnZ0c3N2ffnz7Ofi3tvX1NHOzMvKyMjHx8fIycrMztDV2uDr/WtfWlRPTUtKSEhHR0dHR0dHSEhJSktMTlBSVVhaW15gYmVpbW96+/Pt6OPf3drY1tTT0dDQ0NDR0tPS09PT0dDPzczLysjIyMjIycvMztLW3OPveWhdV1FNSkhFQ0JAPz8/Pz9AQUNERkhKTU9TV1tfZGpvd//58/Dt7Orp6efm5ePg3tzZ19TRz83My8vKysnKysrKysvLy8vLzMzMzc7P0NPW2t7m8XpqX1pUT01KSUdFRENCQkJBQUFCQkNERUZHSUpMTlBUWV1kbXn27Obh3tza2NfV1NLR0M/Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs/P0NHT1dfZ297h5+z0fnJrZmFeW1lWU1BOTEtJR0ZFRENCQkJDREVHSEpNT1RaYGv96t7Y08/MysjHxsXEw8PDw8PExMXGx8jJy8zOz9PW2t3h6O75eW9qZWFeXFpYVlRSUE9OTUxMS0tLS0xMTU5OT1BRU1RWV1hZW1xdX2FkZ2tudHv78+3p5eDd2tjV0tDOzczLysrJycnJycnKysvMzc7Q09bZ3ODm7fl3bWhjX1xaWFdVVFJRUFBPT09PT09PTk5OT09PUFFSU1VWWFlbXF5fYmVoam1wc3h9+/Xv6+fj397c2tjW1dPS0dDPz8/Pzs/Pz9HT1NbY2t3f5ent9fl2dGxoaGJfXl5cXFtZW1dYV1dWWVdZW1hcXV5ca2dje2b9fWDOU8xt1ent1WLK2+br5eFxzO13zD64adbbStBMz2PpY+tzbNR82l/8fV5uZlr2VmRaUFxJWEpUTVxLTk5OW1jrXPd1XN7rfNrn1d/oz+jQ2cXUydLR083M0M3c5tDP381m091mzVzg+ttm2VxYeVNPSUtCRT5APT5APUQ+PEJAQkpGXVRS1mPWzdLGyMm+xr3Fu8DFusq4v7y+urq8tb20vbi8u8C/yMfc3t5PW0ZOOTk5NS8tLywpLioqLyozLjc7O0BXX33Zz8PDvrzHur/Evcq7zL6/usC1rrWutamxsqq0r7u1tcXJ1nZIPz0zLy0oJiMjICUfJSofMjAtTDvnwWqxu7atta6zsrq8u8a/u725x7Wuv7Gstq6wr623s76+1eZbOTwuKyYgJB4eIR8eJyknNDZe+V2wsLWtqaitr6qyv7e2r/nWp+zUrbe4xKyrwLmruOK9vllKNTguHiYiHRwhIx4kKTYzN77Jv7ito66vpqq41rCx+dLFsvrhrb3Bt6qwuq6vs8u5uVJGOTgqHyMhGSUmFi8rI0dC1tXCrquttKKtyrW8v+3dwfLVyr7BxLOwr7iwrr66u8XpST42KCAsGB48FCA+JSxSzde7v6WmwqSnvsu5vUrwVcZxRbS627unrr6sr7TCwb1qNj0yIyQXJDETJDwpKly3z7K8o6HHpqO2UL63NlFO6Oc9urTMw6eoxqyrt8LBw2Y5MzUkIRoeNhsdOkUtS663rrmnnrO0q64/T8hAPDfV0l3Fs6q3rKSssrWyw+ldQDQtJSUeFS83FybETSy4prKxtZ+jv8CrvC1KfT43O9XGv8qupausraWuysO92jg1OCsfIBgdYxsY0sMvP6OowK2rpKvux71qLTbiPDtPxa68taagqbenrbzf78A+Lzc1JRofHy0rGTa+QTazn8y7paqswOnW3T0sQ3s6R8K+r62spKSms6+vzsxnUTs0OiEeHR0nLyYcT7Q4YrGns8KttKu8PO7XQzQ83EvSvsuoqbKrqKa5vLbYzttPRj5HLCIkHCY7Jx0tyUM/yMO2tbrJvKvIR9zdVlNWTWi7vMOvra6xrqy3u8LLzGleSlFLNjErJiYtOSkqNTZDd95c6r3HyLrExMfXbmfDzfbY1r23sra7srW0sbq/xszV7uv8Y11KQDovKywwListMzg6WV1Q1srJx8HE2sfDztrcyMK+wcq+ta+xu7/Bvbm7wMzW5PNpX2dPRj06NS8uKiwtKy80OjtIeGPfy8XIwL/Dwb6+wsTAvru+wL+5tri4vcbFxcTI0d9rVVxjWEtDPTk6NjMvLCwtLjAyOD1BVnHs1cjBvry7vru8urm7uby7u76/vrq5u8HOzszO0t9rVlVXUUxFPzw7OTg1Ly4xMzExNjk9T2Fu5c3LyL64uLq7vbu4t7q8vsLAvr7Av8DDx8jR3eDvZVpYTEhKRUBDPzs4OTc2NjIzNjU4Oj1JSlr518zKv726urq5ubm6u7u9v8DAxcfGx7/Fzc/V3O7nZ1pUTklFRkFDQD05OTo1ODU0Njc7P0NASVdr3NnGyMG7vbi7u7y5u7y8w7/HxMXKyM/L0Nzs7OBgXVBOVUxLREdAP0NAPTk7ODY4Nzk5PkROWm7p28bFwr28ubi2t7q7uru9vr7Cx8rJzdbe9n1qaFpNSEVKSEVBPT48Ozs5ODU0Njo7O0BETFr85ufPv7q5ubi3trWzs7a5urm7vcHJy9Pa4vxsXFNSU05NS0dFQ0A+OzczMTEyMzM0NTk+QkpTcuDPxsC9u7i2tbSzsrOztLa5vL29wMfM0dzne2ZiW1RPS0RAPjs4NTEuLCwtLC0wNDc8Q0tZ38vDvLi2tLCurq6tra6vsLS4ubzBx8nQ3/B1XlZWUktFQD07ODUwLCopKSkqKy0vNDtEUHPVxr24tLGvraysq6usrrCwsre7vsPKzdXpbGNbVE5JQT07NzMvLSknJygoKSstLzhBTnjOwry2sK6tq6qqqqmrrq6wtLm7v8XIzNbj6PZtZltMREA7NTEuKiUlJiYmKCorLzxKWNzFvrqxrq6sqqmqqaqtrq+zuLm8wsfHy9XW1t7q/FtLRD02MS4qJSMlJSQnKSouOklV5MS+urKurqypqKqqqq2vr7O6vL3Cx8fL1dXU3u5vUkQ9ODEuLCciIiQkJSgrLDA+U37Nvbq2r6ysq6ioqqurr7O0uL7BwcfMy83W2NfgfWFNPzk0Ly0qJSIjJSUnKywuN0po17+5ubSurKuqqaqrrK6ytri9wMHBxMbHzNPT1Nv5V0U6MzEuKyUgISQlJikrLTNG+9C+ubi1sK2trKurqquvt7zBw8THy8m9ur2/yc/NzflCNi4sLCgiGxsoJR8nNEc9Wb+8rKm3sKqqs7Sqrq6ttr69xWR9xub4vre9vbfCxsdgTDwyKyknISAcHisfIT5HOEWvtsuop7OtqrC4rK+8rbDIv7ntbsbbbci8xr+7v8jJ7VxINjEtKyQiICAhHSs2JTvJ4Vawpb2upK60ra22srC5s8LKwdxv28r/yLvCvb28wchub0k0Mi8qIyIhIB4gOCcnz19HxKe3uqOttK6ss7SwtbG9xbjV/c3PYNy62sO3xMPCyVnxQjQ3LiokJCEgHyI4JC3HQ1m4qbqvo7Kurqy0trC4s8O+uXvXzNhb1Ll8wrbHyMPBTmNNMzQvKiUkISAgHjUpJ89JTbuqt6+jsK2urLG5r7W1wLq908/Sz2LWv9jKu7/NxMN4V1k8MDMtJiUjHyIdKDkfQdJD1K2ruaWprK2vqLu6r7HLvrL40snR+Fu9zOq8uMvKudx9Z006My8rJyAiIB8dJjshOMNc96untqamqK+vqLm+tq7Py7Hg58vIaVq/y9fEtcfMt9jqakY6NDAnLCIeIh8fHDovIsnH67mlqaykp6m1rq3KxrDK9LjFZMu8XHC7xXW8svS+tdnP1lQ8PTMvLR8qIBknIBwmRDIvs7DHrZ6orqSms76ttFzFsVj4ts9ayr9jzrzGy7i+1LbBfeN4OjE6LjAgICwaGigpGzDEMH2pq7mmn6mvrKbC1bG7TeO172XFyM/lvb3Au7m7u73JuNtiWUw3LT4uKSceIiEaHjMoH7i3P62hqbejo7C+sqtW5bbDQNGz8m6+t+fAsb+7ub/AxtPLyEhHVjcxLTkrISEeJB8cJVcrMKK2xqieqLenqrD7xrHPQdK0TV61u/3ErcDJtLi9v8zfzW5Y0kw8TjouMTQoHyUfHSEjLC5B0qWz0pqjwq6hvEy30N/Z3u28xVqtrcW6rrTFv8C+cUzIY0HezFNLaEs5LC4vIR0hIBwjKi083a+etrWVqcGtrM5Ockq9YkjKr8PPqKu0urW9vGZEw2I/UdZX+sreyeFKPjorJygfHR4fICUtO+mwoJ+yopm0yLnDQjw3ULj83rOnsraqqa7IbObnOC9IVT5N28W7v8u2uVU8NC4kIB8eHB8iJC02WM6soJ2trJyr2FfVTz41P66vvLWip62vuLC8QzRLPjQzRlnXyuG7r77TvsxSMSooJCAfHiAgIyorNEDfrp6mtaCfq7/Bz95WM2e4r7uvp6eor7Syuk09PD03MTdJXGLgx7u+yM/G1EU1LyokJCIiIyIkKi0uLi+/qa+zqJ+iqbavrL9RZsK3t7yupqWsr62tvFo+QDw0LjE5QUZBUtfUdeHq6HdIOjArKSooJyknKzIsKC01PvF6y7Gqq6mmp6Sqr7GwuL27urW0tLGtrbG2usLdXEM7OTg1NTc5PDw/Q0xJRkU+Ozk4MzIuLzY8Ni8uMDUzLS46RU1T1LqysrOtq6yusK2rrrKwsLCxtbSxsbe4urzAzdRgVkxGPT0+PT89PT47ODY4ODg4NTc7Ojo9PkE+PTs6OzlCOkdDTEpXXfva0cjHvL69uba7u7e5uLa8uba0trW2trO5u7q2vcDWzM7mXUxXUF5FTEFZQ0M/PUQ7QzlCOj46Oz0/QUE/Pkw8Qj5LRU1HT1pc713h/tN4097Lz9XGzsDIxczFysjEwMLCzMTKxsjIytnKyt3W18rfXurleFhVWnlRW1v0Xl5iT+1aX1xWcVd8bfp9WGLcUPVe7Gt4WXD56VbgeGnoX3DuXHfibHrtbFbi+u93b3X0e2x+9d/kWHRl6mfh6Odg+mbi2dHj5N/u013VeNVe2WDa8HTj+dX09G/RZeBv2F5k+t5nW+n173p3cmvqYH5pZ/xPdGXUV2lj6fliaGTvV/Bq2nf+UnZg63v5aVTv5HJsfGVgeG1haGJ1XN9tfllc8/jvXfFf22D+Ydp6b21n+mDcZ85p41/vdPz78Gt769tt6N9s31t6/fh8Ymt5d9hoeuDjVW/o7GVe8mbjZeFu7GxsWXbiau1sb2HrfNp1fvvvZt/2Ymz5etngbvp35GNj53tsbPh5Zvlr++X4ff5+4Hhv8+PqY358YmbrX3dg6PBh8Olucm9u3mRsW99uY1564VbWTHPk6HJxefjxYer53HRrdOvgY2Xt5Prfafzi7exu6mvdZmzsfHz64Wd57eBnde50fm3+4v7eV9tcc/JXzVblfXN8amJs6erlbttY/m707HzXYHVzdOD97ehxcHpd+f7XXn1hZ+li7nbg/1p8eu9n7XHwdmJfaHXdbW1p+mhy3vjeb/Jqd2loduprb+Bv32/qauP4dOhd/nbwcnj302ffVtbu32dte2La/N/l7WJw6uPsavxr6u9j/fDt4ltqVtpy/Xv13mzsX+dg731t72dkWev+4Gp9+XvkZ/DzfuptbnDXV/zs51Nn7PL5T9Hn6lPfeHF0X+Bh7FzQTtJp4lTlYXdrbe/o52x47OdZ52Xv5GhtfG/eYttu6eR5addnW2zi9+npX+ZrbHrdZttUam/s4mTod/NtbWjXXO/vc93yeFTcXfzpVv/n6VPOV9dk8+hm2l7QT+pw6uBZ2/TfUN5Z4PHl8HbPZXxr6mN7Unj0aWdk9WjgXNx83G5k6eriV/dx/WppbvTb3Or86nL+a2DcYWVdcdtt413s+uF43dtlb2LkYG5e5fvfXOX+ZulZ3u/XXO3s2XrrZubnX/VlyuzV3tjn9uB039th53DpbOdrYvlV5l3cV9lw9W1V9mJ8TGlP7E5uXF5mSVM/XktURl5OYFxIS1BLPEI7PD9FPkdJT15ec9PCvbW0rqyusa+wsrW5trS2vb28trm8wsHDzNtyZ05QQD47NTAtKSgmIiAfHyAjIyguPcisrK2pqKqwymvxTTo7SuHAuLCpoqCho6issLfAzdvSycK7vLy6vsvuPjArJB8fIB8fICMnJiouSLCntbiurK3HOjNAOC8wP8mtrKukn6Gps7+/1kQ9Vsm1tLWqpaqwveRINykhHyElJCUqNzcsKSklJS7eq6O30KOnt1YpKTk4KTnKp5+ipqGisuM/OD1OSO6wpJ+fp6qrvUY3MzAvKywzPTg1MjQ1LCklHx4gI0Gel66zpKu0NxwdMjMzUbaemZ6tr7PsMSksRMe+s6efnaOvvMLNV0BCWm9JRD86NS0nJiouNDk1MSsnIiXQmJrqt66vviIYIVRLb7ylmZup495VNSorRrSmqaqoqKy9Wk3LvLm5tre+YjIsKyspJiwyNkBhZVY9LiUeH02Xmsm+razXLRkaZ79iv6GbnKs5N1E5LC1Qq6CmrK2wvVg+VOS7q6mprMJCLiUjKi40NTk7ODtDQThObSgcHTubnL7FsKfANx4bP+M73qObnavvTPU/KipFsKuqpKSrxz4/0NZO/7Kjo63LOS0nHyAvTehULSk/TC8lRq/KKhUoq6OzU6ulqr0oHzA3KDmunJ+nqa7FRi8mNeLSsKKgoqxkRMvWPz/LsKustbneLyYgIjJXPS4xSVooHSewpTAfLEhQ7b++qKGuy8VqKR8lOcKvqqOenq1fPz8zLz5duKGfqK+2xs7iPjhiuri+zciy1yofJDAzJiA7s1UZHci6MChvUD1HOr6otsSroLFENTk9LzRfuKioqKOjtFxCPUNRTXu0qqaqsLO91ElQz9JVPEfCu0s4QTUqKSYjLzkkHjE+LDDS+DFGxcpHXruyrK2zt7O/XFBkUFPPvrmrqKqrrsTr4FZn29fNuq+6u7G1yMPHdFdGNjI8ODI6Pjw2NzMsKiYiJigmJS9CQDjxr8dXw6u0yrGstb+yscHCub7QyMC/vLi1srK7u7m9xcrBx9Peztpka9bsVu3jUj5EPzUyNTQvNDk4Nj49Ojs8Ozw8PDw9PD1OSEdd4PTnyMrOx7/Fwr6+wb++wcPBw8nFxMnNyMjMysfKzs3Q2N/m7PH9/3xzdPf0bmlqZ19bXWFdWF5cWFhcXFZVVVRVWFpaWVdcW1dZWFdXWFtaXl9iZGdlZWdoaWpxd3V2ffr08u/w7+zr7e3p6uzq5efn4+Pj4+To6ens8O3u8O7s7u/s7e/v7fD08PDx8/D4/fT5enz+dW1ubmpqa2hmampoaWtqaWpra2xvbm10dW5weHVzenx3d317dnt+e3z8+v348/f48fT38/P08fLz8O7u8PDw8e/x9fPy+Pz3+P1++/t7fP/8/f7++PL8fvb3+vr5+fv/+n389f359fL0+nz5+nz9+3p4/Pr6+/r59/T09/v3+fjz9f767Xt38e7++Odx/u758mx37PZ2+fLx+nl0cfz8buxpa3X2bnbv/03azEX2zF5U2PFo8+5saXT87mv97m3r+nr1Z/dt73f3+1z941ztfWJn6mtZ6F7v0vpf2npa2fpN6tVacO9cXOTnev7nduvobuPha3lv+OLmZd7hWdvocPNySd7ia+P5Yl16+v5u/X1Y3/J9cXnsVG5jZ1pdd/N73X3w2mrn6Wp8d/tmau3wbv3uZdre6+zs5e/pdWVsbWhefGRn3WPb1d/t1OD02v1pZ11oY2P8Zmv6Xv7mZV1xYmhrWm7qV/Ji+1lk/VNkZGBbcVxT2nBg935bc/xj6Gbd1frUzc3XyMTRxcrKyMfJycbIx8nFztXN0NTl39x2UV9YREpNS0dXXWjez8nX0dBlS0xANTQyMC8xMzM5OTg/Qj9HT0xFTk9JSlRsVWnWz9LFv83BvL/IvLbFvrW0vLeusrOwrbKvsLu4vMbDymdq6ko/UVBFSExLSD44PTgxNDs0Nlla6sW+vrrF7NthQDxBODU7Pj9LTkZiX0pEVlI/P0E7OD04ODk/RktaXmz92eHW1MrQxrvAu763trWzsayytbCvtbeytLi3vNbPzVlc+ExR3WdN8NZKPUM7Mi42PTY3TtdbbMTC123c0+RLQGPxST9X4GxISH5tSD9IUT41O01KQ0ZWZUtDPkQ+OTpDXltj583X3dTLydLMwL/Bu7m6u7aztLKysbK3vrq5xc/KyMzMyszM2mBtXEtJX01CTUU+OTk2PkY+Pkbx4ODjzcTM5XDq91tQW+TZZ2nZ2lpNS0lLRT4/S0VCSEtNT01ARUpIQkVJTU1ITFFYTUxQZ+7n3MzBw8XHwsC/v767uLa3ube1uLy/wMXJztDMysnMyc3U4NvZ7WZQTUdBPD1AQkFBR0tTTkpJTlNTVFl76ujx6+Pk6PL27N/f6Pj77+12XlhXV1RRT1FTU1BQU1VTT09QUE1LS0xMS0pKS0xLSkpNUlhbZfne1tHNycbDwsPDwb+/wMC/vr28u7u7urq7vL7AxMrQ2ePzdGRZUk9NS0lISEhJSktLTU5RVlpdXF1gZ2VgXmBjX1xcXFpcYWVla3n27/Dw6eTo7vZ0bGtjW1tcWldWVFNVVE9OTUxKSEZDQkVHR0hKTVFZXF1me+/p5uLb0s7MycfFwb+/vr29vLy9vr6+v8LDxcjKzNLY2t/r+XNqaGZiY2htcHBve/Pz/P57dHJrXllZWVdVUlJVWVlZW11ja2xlYWJiYFxYV1teXFtbXF9jYF5fZGlrZmNma2tpZF5dXFhSTktKSUdFQ0NERERFR0tOUlddavzo3tnUz83LysnIx8bHx8fIycnLzMzMzs/R0tPU19nZ2dra2tvZ2NnY2dzd3N3h5eru7vR4cnRxcnZ1d//9+vP0+vPv9v57cm5rZWFhYF5fY2doam54+/r68+vm5ebn5eLj5+rr6ent+Xhua2ZfW1lXVVFQT05NTU1MS0xNTExOT1BRU1NVWVxcXF9lZmZrbm91fv58/fTu7enk397d29nY19bX2djX2Nra2tze3+Pn5eHf4ODh4+jr8vTt6uvo4uHj5+rv6uTk5+Xm8Pbt6/l7/v18fX1vbXn3/Hb46+378uvt6+jp8vP793nx7fZybm93cmj27/Zu+vz8bGBjYW5wemt8eHxlXGFvZGBo9vhrX2BlbXNfXmFkWVhdbvlvcXL8fXr/9Xx2+/p9ffl2/G/++ubi6e3t9Xz5Zmxw7nN7c+7j7fv26e3sbezk4PDr8/L2dXp25ePh7eDk4Ox4/P71c29ffnl4+v39+e9v92x7eHhmdn527PfreP9173x1/vjh6e566PJ7ZnDr6+924uDj9G937ut1fPPj5nlw8uTueGru4fNwX3x+fWZy6+71Zn158npudPn5/XhrfHt3bW907Xx8fHJpbHpre3Lo735xfPxsa2d69fxzdPzs7HBs/O/8eW137nRpaP3s7mxx8nlzYWRp93JqYGX7bWdldv99ZWf8+H1kbX32am11+O1zcGt6a25eZvz4eGv2/PJve29qZGz5fu167vn5bGpwdOtvdnnyampve+5wd3PvfX5jbPbv+G56d/Vv/n5+8fH7fPH++nd5+fjwe3p16+ru8ffp+3pncHr8dXLr6eZ6e/7t/HBrcO74eGz5e/t4+u7v6/Lq8Ofz9/fy+f758vB++u7r7fP7+PV9b21w8fv5/O7u+Ht+7fP9b3337nt5+ujn9/1+7fn7df359n10/e3q9/386uz4c/jy8Hh0++7r9vL36e/6cHJ8/Htxff/0ffr18fl9+/Pw/3t7/fv8fvv4+/p+/nx1dPv18vf7+PH5fn599/59fPb29fj79f7/ev95enh8+3t7dX78/Hz79vl+cXt+fXJucvv0fXdx+/Z9dHR8fnpud378fHX/8+/9eXX69354dP7/fHB0en13cnz7+ndzd/3/dm9xffr+dnn99f12cHJ2dnNze/35/nt9+/t5c3N4eXh1dHh8fXt8/fv+ent8fHp2eHt+fHh4e/37/n5+fn16eXp8fX57e3x9fnx8//3/fHp5enx8fH7+/f9+/vv6/P7+/fv7fnx+/fz/e3p6ent9//3/fv78+vx8e/76/Ht3e/z3+f389/P0+f39+Pb4/nx++fb3/P779vT3+/z49ff9fHx+/f3+/v79+/n5+fv7+ff4/X19/vz9/v77+Pf5+vv7+vn6/P7+/v39/33/+/j4+vz8+/v9fnx8fX19fX7+/f38/Pz8/P5+fn1+fX19fv7+fnx8fH7//v7+/fz8/v7//358e3x9/35+//78/f99fn7/fXt7ff/+fnx8ff//fX19fv7/fXx9fn5+fXx9fX18fHx9/v39///+/v9+fn1+/35+fv/+/v///v78/P3+/////35+//79/v9+fn7//v9+//79/f3+fv9+/v7+/v7+/v39/f39/f3+/v38/P3+/v39/Pz9/v39/P3+/v/+/v79/v79/f7+/v///37//37//35+fn5+fn5+fn7/fv9+fn5+fn5+fn1+fn19fX19fX19fX19fX18fX19fX19fX5+fn5+fv////////7///7+/v//fn5+//9+fn5+/35+fn59fn59fn5+fn5+fn7///7///////7//v7+/v7+//7+/v/+/v///37///9+fn5+fn5+fn5+/////////37/fn5+fn5+fn5+fn5+fn5+fn5+fn5+fX5+fX19fX5+fn1+fn5+fn5+fn5+fn5+fn5+fn5+fn5+/37/fv9+fn5+fv//fv////9+///////////////+//7+/v///v7+/////////v7+/v7//v7//v7+/v7+//////////////////////7//v7////////////////+/v///////v7+/v///v7///////////9+/////37/////////fv9+fn5+/35+//9+////////////////////////////////fv////////9+//9+/35+fv//fn5+fv9+fn7//37/fn5+fn5+fn5+fn5+fn5+fn7/fn5+fn5+fn5+fn5+fn5+fn5+fn5+//9+/35+fn5+/35+fn5+fn5+fv9+fv9+fv////9+fn7//37/fv////////////////////9+///+/////////v///////v7///7//v/+//////9+//7////+///+/v///v//////fv////////////////9+fn5+fn7//35+fn5+fn5+fn5+fn5+fn1+fX5+fn5+fn5+fn5+fn19fX19fX5+fn5+fv9+fn7/fn5+fn5+fn5+fv9+//////7+/v7//v7+////////fv//////fv////9+fv9+fv//fn5+fn5+fn19fX19fX19fX19fX19fn1+fn5+fn1+fn19fH19fX5+fn7//v38/Pv8/Pz8/Pz9/v/+/v38/Pz8/Pv6+fj5+fr7+/z8/P39/v79/Pv7+/v7+/n5+fn6+/v8+/v7/Pz9/Pz7+/z8/Pz7+/r6+/z8/fz8/Pz+fnx7enp6eHh3dnd3eHl4eHd1dnV2d3d2dnV1dnZ4eXp6ent7e3x8e3t7enp6enp6enp6ent8fH18fHx7e3t7e3p6eXl5ent8fH19fX19fX19e3l3dXRzcnFwb25ub29wcXFycXFxcXBvbWxqaWlqa21ubnBzdnr++PLv7Ovq6Ofl4+Hf397d3dzc3Nzc3d3e3+Hj5efp6+/2/XlxbWhjXltZVlVVVVdYWFpaXF5gZWlra2hkYF5cWlhVU1FPT1BRVFZYWVteYWdtdv/38Ovl3trV0c7MysjGxcTExcXGxsbFw8LBwcLEx8nMztPb7mZVTEZDQUBBQkNERkhMUFheZWlqZ2JeW1dRTkpIRUREREZISUtNT1NVWFhZWlxfaHby5t7a1dHOzczLysrJyMbEwL68uri3trW1tre5vMLN4GJPSERDQ0NCQkNFS1Ji++ni5Ont+HVoW1FKREA+PT09PT09PT0+Pj4+Pj9ARUlNUVZZX2dxeXZycfzr2M7Gv7u2sa6rqainqKuutbzG0N9xV0pBPTw9Qkxa/d7SzMbBv8DEzd9qVU1HQj47ODUzMjEwMDAxMzY7P0VISERAQEBDREVGR0xb6Mu8s6yopaOjpKeprK6wt7zNcUtBQ0hRWGBhbOTNwLy9v8bM2d/c3eheSD03NTMyMTEwLy8wNTo9PDcyLi0tLi8vNDlE88Czqqelo6Skpaepq6+0vNTpYlPv3dbc2+TTyL68x9xgW97Qy85jTkA8QU1mXkg8NzA1ODk0KiMgICAhIB0fKTi3pp2boKCopqerv0Y2Lku/qqmxr7Opq7C9X3Db7kkyMFyuoabBOi82/ra6XywkJS1QT2JAJiUmIiEbExoiSqimnZ2no6y0sFU4KytGrKGmpLO+qqyxuzY3RcmvuNxAVcu3tr/BvrnIPSYjKTnAyVI1JSsvKB8YGB8rRr67qpybmsshLSrLqE9C66+Zl63ITtupuzkuL0GsrcXJeLWiq9M6Jy0+PERLYMLA4EQvIx4cGxYbJ6uPoqx+LauwOyghLaafrrDPrpufsVcsONlZNS88tZ+msLi+wNU1JCw+1sNwTE9L4U0pIAwPKUijpLSqn7G7NB4yPefBdManoJyhvL7DSzIqJS/TvLKop6OjvDgpLkxJOisvuK6bLhsiFBwbIjekoaCjzb3MXDIvLTi8tb6no5+du00/LCoyKjW7rqCeoq9aN1NwOzMVLaKnpyoeKSwcHCI7pqirt7WsuW0rKzg6P+WwpJ2epblXOSgpKytHtaKdoK3YtcdZOREdx7TzMzJQyR8dK0q4xElao6Kt4i06Xi0rRLOcnaWrsL1HJB8nL+atpKGnsKynTiYaGsnCKCxE2NMuIUi62UtAe6apdkBEUkcxM72moKitsLnSLiMoNU21sLCoq6ut4SspKzk1I0dIMTY9wrz/NfG1uc9T58PfNS09ybO0tqqmrOpDRTMtL0e/t8a0qaq0Wzs+QC8oKTE0IDPDvLlwyaypylJp5GgxKjJ0ysC2raaou/djWjwuMlXMxsa8rK/F3VdLRTcuLC0rKSRjrGzNvq+jrHpUvF42Lio+51PnuK6qssi/uN5CO0LtT0FZz8TFztHF1EpNS0FDNi0wPEbR8UzIur68vtDByWBhXlty61BVatrByOHwzsbO2fXwz9to7+j6a1VTZnRgW2zm5t/zWltVS0hJS05QVm723MvNz8vKzs/Oz8/a3tfU1tnf5t/Y2OP3cGt2+ffu6ufd3+no8HBiXVlXWE9OTktKS0tKSk5WXGr35dfQ0c/OzMvN0NHT1tjd3+Lt7uXm6Ojq7Ovn63lrbnl9d2hdWVZSTUpHRUZIS05SV2Bxd3n+9evj4+Xk4Nzb3NvZ2NfU0dDOzczO0tbY2d3j7PDs5ePq+mxfWVNOS0hFRUdGR0hJTE9SWWR47uDY0c7Oz9LV2Nve5Oz28Obd2NPPzszKysvMzc/R1NbY2t7o/mVYT0pGQT89PTw+Q0hLUVtq7dzX19jZ2djW2uLr7uzn5ufl3NPLx8XGxsXExcfLztDT1tng+mBVTUZAPTk3NjU3Oz5BSFFdddvOzc3MzM3MzdTe5OXn4t7a1MzHw8HAv7+/wcLDx8zR2+t7Yk9FPjs4NTIvLi4wNz5AQk113M7GxcvNysjLz9jl6uHb1tDOysO9u7q6uru7u7y/xMvR2+pnT0Q9ODQwLiwqKiotMzs8P1Pr0sW9v8XCv8HEx87a2NbZ0crKyL+7ube1t7m4uLu9wcvY4HxWRz03Mi8tKignJicrMTg6QVPuzL+7u7y7vLy9v8bIyc3Nx8TEv7y5t7W0tLS2uLq9xM7belZIPTYxLiwqJyUjIyYrMTc5QFfdxry4ubi1tra3ur29vcC/vb29urm5t7W1trW2ubzBz9vcflBBOTIuLSsoJCAeHiQqLzYyP8+5trmysbSusLm4usTOv73Mv76+t6+xuK6ssrKwtL7C1WRlWT00NC8qJScnIyAbHCojJ0nqVfGkrMqnqbu7sbtuuMlGwMDdwrm9uq2wua6utbO6uLbN5dbdOE5NLjM1LiIsLR4nJBsnKSIttjo7nLjGo6C7vp7D1q+7YfO32ujAusS4s7G1vay1z7S1a/vGST3qOTM4MS4mLykgLCUcJTgcMrow766m+qWcxqqlrNOrr065x9jpz8TOxsqvz8KsysO0vFTNyTpZUDk3NzUrLy0nLCQnKCImKy4otE5Dn7SzqZu0sp25ubGqWNiwQX3KwFHKtWDAub3Rvrhb3MtYRFtNMz46LS8wLCgpJyUlJiUxJj2tJ6yhzamen7+dpLmsq65IscFAab7jP7jKXMm2zN65xGHpxktC60U1Pj0tMDErKigoKCQnKC8nRLUrsKPDrJ6gtZ2lrqyqrGiwxU5yzvk+v2tXzc7M9r3Nbs3QTltyP0Q6OTUtLy8pJygoIycoKywstzpLoLq2o5yypJyuqKely7WzX35ewTVYxjdtfuBY3Mpq89b3Rt0/S0kvRTMqNS4pKCsoJCoqLSZg3SutrsurnqatmqampaGqu6fD19bOSDrZOzlUUD5S1khr3GtZXGtGS0w7PDwvMDYsLCwqKycoNSQrvS9Dr7m+rZ6rpZ+fpaidqrKpr9XMvklNUkE+PkZGQ05dS1VwTU1WR0tDPkA6NzM1MDQvKzcsKDkrJkhDLdDN372wq7Cmp6aqqKWtra2vvru8z9zb61BJWUk/R0pAP0tBPj9CQDs+RT48QkM9QkRIP0dLQUVFPztEQUFHTFFl/NnMyL++u7e3tbSztLSztbe3uLu7vcLFydHq+2JPSkZDPT4+PD09Pj4+P0BBQURFRkZHR0ZHR0VERERDREZIS05UXGnw4djPy8jEwL++vb28vLy8vLy8vb29vr6/wMLEx8rN09rk+2xeV1FNS0lHRURDQkJCQkJCQ0NERUZHSElKS0xNTk9QU1VZXWFpdfjq4tzY1dHPzs3MzMzMzc7P0NLU1tja293f4eTm6Orr7e/z9ff6/P3+/fz9/318eXZ0cG5ta2lpaGdmZmZnZ2hpamxtbm9wcG9vb25ubm1tbW1sbW1sbW9ucHR0dnv+/vn29O/v7ezu7O3w7u3v9PDy9vPu8Ozt6+ro5OTp6OLp4+Xn6u/q7vL1+vl8fm5vb/RufV9zcW7mZ3p2aPN8b+9u+ml6+mN5ZHZrW+xefHdq3lfs1k3bYflwcffodeJsbVjoYdh2buBIynhqcOVYdG7uW/hg3Fre7UvXRNT2V9FO1mrSXPzhUNRk61bs6WPLXMxW3F/b/uh94Wvhc99a4+1O81TW71PLSM5YyVbZ11vI9Gnq8GZde/3kTNNc4E793lDMaPj5Ws9U389HtTfK0EDJUMxa7tZOyEPBP8t1ad5Uvj7L+FW+R8heatlPy2PoXtZIzUzPUH3NSstA0ulEzVzZT+t5zULM61G+LaglqTD9symrLrA5Wr06ujq+30a7N7NAWMhIyjq8Rf7COMDHOMbWcj+yNdPWQcFEyz25Q169ObpMadJt0T+4P97IUkO4Nr5Y20i6SUOvN8zS6EDAT9h0bm5EszzOyjzFQ8naWM09vDnG2Dy/Z0fRSshN0knl61LbPbY0uDy7QsVMTLYsrzXFZE+6Ocj6WdtmWcM0vWBcvS+nKbPjMaUlsj/awyupLrrmLao03stIxES4NMXROr1Lx0C/QL1NPLEstUjnXtFeQLc6yOxM8+JXS75Ya02+Z0e7SvTNRu5uvzrquTxYuTm5PMNgw1M3pinLz1tP3bssqzPM3UnMXOjPWtbj5EGzXz6yPlbMbUPN2GPrTbU8yt/PTUKpLGe3PcdJujK36zKtSzm+WWxOwD7ptC/Izm9H1XBOzGpzvUNLuUHM00LuujRUrjlLwc1M9s5Jsi68z0/1RslSwUNFvjvD5EzqwXQ/u0hcuko9v/45vElXwTvI7MEtuccknibevkjbN6ktzsc1ul4+tt1Hc8hfRdRBz7kz079jVMnyPrdNPqxGNcC5NEyvNva7OtO/OEavQTW3T0/Dbz++1jG/2T27SOFX7+/4zj21dVC9ScHuZ0/f3EDNTlrST1Di1Uj8yXg8yFNM3EpXYe816r4v7UrfREbSPb5AXNFJ20rLUdDdTs7V9dm538mwvLC1sbOsrr6rtLyyyb683dBsTERORy04PS0wLy4tLS4rLywtMCguMi0wNDc9UE14ur+8o6Wln56en6Knpq2/u8zSbG7Q3s/LvrW5u7i3vdPzTz81LS0oKCcjKSUkJickIissLDA1Pz9Nzb6woaKkn56gqauvvdZSWU9CR/PJv7ivq6qrrK2zxdxYPjMrLSwoKS8wLC40MCwoKSknJSowLzVDU8qypZ+cnaadoLjB9144LSw4T0fVs6emqKOhpbLP21gwKCorKyovQ01Lbs1cOzQtJiAdHiEkKzT5xMSzqaOemZywqKLJPzU7OC4vPcG4t6qgn6asrrPFQDQ2MSwsN1BOXsnAx3hXQjMnISUhHR4mLDRN57SvtrW5vMCwpKDCeqCr1T1Bxk0/OMitt7mvoaa3ycu+TzAvO0Q5Peq7u87DvOE7LiwnIR4hKy8wNEvrUlTFvMjdXG9Ud7Wenc6vm6LtMOtDOTIq0amr162co7dX7Mo5Kio/dkRExqqtvtjXyD4pKCkmIyApSkMyOVjQQzljwddO1E4yabWgpMKgm6DMPvUtNiojXKyot6WcobDSYE8yKigzy9lSx6qlreU+SDwsKTRHNS9AQzw8NjAtIyRBXTxAsZ3BOyhapU3ZfpuYrKO4uGkvHSNUZEy+nZyoravCQjEjLElHYL65raKxVkVCLScyS8PHVdfcPDQ0KSQpKy4kJEOqo0v0sc03OVo0zbCvopeZqbHFRDAnIytKvbSsoZ6owWs1LDhIWdm/sKenxz9DQCskJzmyrFw8s6hbHxozsvYdKm89Ly0yOLltJqafOzM9czfIVLyWmp+4o7I5LB0sV7fcv56ir0hXS01ALl6/p6yyra9XHixGPT1VxLm79TzSp2AsLS48Mi4pPsXfQSYkLjs/RU5Ovbu6tOPTJCbDqZ2tqqWfqUosK+VJMDutnaO9T8fH1zArSrmusa68tMRALClLM028xMXMuT0uyrhOSME9LSkeJ1tYOfZZNC8pJC3uycjH3bm0TkI0blRAoZyZrtzFutYmKDetqr+7p561Nyg/zMdnVLKkqM3p0tdIKy9HvsNY0rzB6kk2PdS/1cy79jEkHiM0RE1gXz0yKSUpMDtQxbnL9HBHT0MvL72fpZ+ctDlD2FYxLk2uo67Dxa+5QzA6vLTG166nrsFI7cPNQztl077Gy8jCy0hBTGR3zb67wlM2LzI3OjQyQ1I+MTE2Ly0uLzlNaVXgYzo0Li5OvtrMrKaxv8BTVO1KW720uby922H17fPfw727vLm1utdnyr6+v7vHaWV+xbi5ytDN21Y8UuPZ3dDJZlBGR01IOThGSFJWRz4+OS8wO0NCR0ZKbXw9Mjk+Qj07TMPUTdvE09vI2GvQzXfPvMDLxdxeycPN+9rX0b7Dx8e+yNO4tr67sa65xtLk3dz44cjJcEpNTk1QT2Z6Vj49S0A3PlZnUD0+PT1GRk5MYkpCTVxTP2LS72bbXVjeen14YO7GzWdPd9nO8/q/vcR20L3E1u/Ex1s+Usl6TdnEydzgcFBjX/DGwdHMyGT+xfhF5Lr1Qlp97nlMPunORDZOb0pHQUhGQDlG21hCT3ZUSlRtU2xWVvbX2Er7xk5MzddT7bzsU37z4t7Z1tHKb1TEzfTbvL3M3unGysvUxMbRyMrLzcjNyb7MXs3C0u7NzXjp2/9cz+FjWU9PSFNVYF9PXVFJSEE9SvFOTfXeUkVkSEhGRmHi61jsT09ZUE9p3lRc62Vb8PBRTenl4HZw7edcVNxrWF7V3lVm6N5reefe4t/z5t5ia9zO/Ozb2Hdy1dvh/dbt8dnf4+rfadrnb1V3b1hmfV1lXWVsVl9+XU1PbFxVVP93Tk5qYU9X/V5TXWNTWGxbWWp9WVrt7F9n5fdncebibnrh3vv55en7593q7uDg7eLb4e3i2eXr4N7m5t/f3+Pg3uDm4t/j5eLf5OTi5+jl5Ons6+jp7Ozt8PDx8vH0+ff1+Pj3/nz/+356eXp5enx8d3d5eHV3dnFvcnVyb3B0c29vb25tbW5ubW5ubm5ubWxsbG1sbG1vbm5ucG9ubm5vcHFycnR1dXR0dXV2dnh6eXl6e3t7e3t8fX5+/v39/f3+/f38/Pr5+Pj39/f39/f29fXz8vLx8PHw8PDx8fDv7+/v7u/w8O/v7+/v7u7v7+/w8PHx8fHx8PDw8PHx8fHx8vLy8/Ly8fHw8PDw8PHx8vLy8fHx8PDw8PDy9PT09PPz8/T09PX39/f29vj6+fr7+/39/f3+fn59fX7+fnx9/358fH19/nx6eHp8fHx9+/z9/f/9+/1+enx8fHx7dnn9d3n8/37+eXBz+XtucXZ7enpwdf9+eW96bGx+b2l0d2doandwZGBxXmd+anJqaGZzS+vGTEfMzERT0/BO2dZcc+pNWcXdR23IU2HKzl5Xyc5SZehcWV5zWnDe7HDw2ubg8GZhXWz55uN85d52fuX/ZOvd6nHi7F9y6v9h6OXz7ez+393m6+F1eODaffbo9H112vXj7evq4tvn7Wfs4v5z6/Bu82le/Wdabublduj7+OxbbGFXWWdfXXZjfmzk3HZy+Ghc7HllbftYXfJpafH+82hr9m52Z2h5YV12Zlxp6nX7aGbmd1/sdXZnefNc7Nzh9ubg+mni2WVo2udg5dhoe97Y3uzm69/ucOx7dm918Xzp3N9n7N5nbnheTf5vXG3xdGfx/vdxdnJf52lYX3r/WG3k5mJv3fNx3nzv3nPm5nn2fH7t7n7v1d9r9tXpZHXn62j96fJt/PPuevXZ8GHf1/pp6Hj+eP3sdWFs5m5z6t7yYOHYYWf6aW1y4/5u3dxrb95veOjj8GX43vBu5tzfbmve62dt5uxb9+z3aXnaeOx47HFaZnBlW3R7ZFrk815ofmtZeGBaXlxfXmj2/GTz7elpafB4ZnB8X2lobPV6bfHofnvs715e+PhkV3j1YGb79mZhbuh0Vm3tal1x9nRhbHRuYF36/mVm8X5jZXdyXG50X2Rwe2ddcnprZHP0eGxjevZwZXjzZWz48nRu8+b6dO7wfnXx73Bt8+tpbeji+f3i7/7+/fRwcnrve2/w8Pp46ODvePDf8G357n1p/uXyefDd5/bw4+V5++7renD17/r+6uru9u7q7n167fL8+uvt+n7w5O1+8+jv+Pfr7u7p6+vu7O/s7Oz08u3w8+7p9PPt6urz8uzp7/rt6vD78+nw+vHp7n716u38/O7q937w7Pp58uvx+vLr7fPw7e3x8e/v8PX79uvt8fDp6O7w6+jy+vDr9Hz97uz09e7r8fby7/Z+/Pj3fnz8+ff3+Pr6+vv9/v58eX38/Xt7+vp8e/v5fnp9+P53ef39eXn/+3l2ff17dXr+fHV1e3hwb3h5cm90eXZwcXZ0b29ycm9tbnFwbm5wcW9tbnBvbGxucG9tbnBvbW1vb21rbW5ubW1vcG5ub3FubW9yb2xsb3FubnBzcG5xdHNvbnN0b21wdHJvcXV1cXF1eHNwc3h3c3J2eHd0dXh5dnZ5e3h2eHx8d3Z5fHp4en19e3t+/v98fvz8fn39+/v9/Pj4+vr49/j5+Pb2+Pn49vf5+ff3+Pn39vb4+fj3+vz7+vv9/fv7/f37+fv9/Pr7/n7+/f59fv38/379+/x+fv38fnx+/v99fH7+/n7//f3+//9+fn19fX1+fn5+/v39///+/f9+fv7+///+/f39/fz8/f79/Pz9/v38/f39/f3+/v7+/v///v//fn5+fn19fX18fH19fHx8fH18fHx9fX19fn5+fv/+/v7+/f3+/vz8+/v7+vn6+vn5+Pn5+Pj4+Pf29vf39vb29vb29/f4+Pf39/j39/j4+Pj4+Pn5+Pn4+Pn5+fn5+fn5+fn5+fn5+fn5+fn6+vr6+vv6+/v7+/z8/f39/v7//35+fn59fX18fHt7enp6eXl5eHh4eHh3d3Z2dnZ2dnZ1dXV1dXV1dXV0dHR0dHR0dHR1dXV1dnZ2dnZ3d3d4eHh4eXl5enp6enp7e3t7fHx8fHx8fX18fX18fX19fX19fX19fX19fX5+fn5+fn7////+//7+/v7+/v7+/v7+/f39/f39/f79/f7+/v7+/v7+/v7+/v7///9+//9+fn5+fn5+fX19fX19fHx9fHx8fHx8fHx8e3t7e3t7e3p6enp6enp6enp6eXl6eXl6eXl5eXl5eXl5eXl6enp6enp6enp6e3t7e3t8fHx8fHx8fHx8fXx9fX1+fX5+fn5+fn5+fn5+fn7//////v///v7+/v7+/v7+/v7+/v79/v39/f39/f39/fz9/Pz8/Pz8/Pv8+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/z7/Pz8+/z8/Pz8/Pz9/f39/f39/f39/f39/v7+/v7+/v7+/v7+/v7+/v7//v////9+fn5+/35+fn5+fn5+fn59fX19fX19fX18fHx8fHx8fHx8e3t7e3t7e3t7enp6enp6enp7e3t6e3t7e3t7e3t7e3t7e3t8e3x8fHx8fHx9fXx8fX19fX19fn5+fn5+fn5+fn5+fv9+fv////////7//v7+/v7+/v7+/v7+/v7+/v3+/f3+/f39/f39/f39/f39/f39/f39/f39/f39/f39/f7+/f7+/v7+/v7+/v7+////////fn5+fn5+fn5+fn59fn19fX19fX19fX19fX19fH19fX19fX19fX19fX19fX19fX19fX19fX1+fX19fX19fX19fX19fX1+fX5+fn59fX5+fX1+fX5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fv9+fn7///9+////fv///37//35+////fv///////35+fn5+fn5+fn5+fn5+fn5+fn5+fn59fX19fX19fXx8fX19fH18fH19fX19fX19fX19fX19fX19fX19fX19fX19fX19fX19fX5+fn5+fn5+fn5+fn5+///////+/v7+/v///v///v7+//7+/v7+/v7+/v/+/v79/v/+/v3+/v7+/v///v7+//7/fv/+/v7+/v7+/35+//7+/v7+//7////+fn7///7+////fn7/fn7/////fn5+fn5+fn59fn1+fn5+fn5+fn5+fX1+fn1+fX1+fX19fX19fX19fX1+fn59fn59fX18fHt8fH19fX1+fn5+fX17enp8/H7/+/v8//0=";
       
        var holdOnAudio = new
        {
            @event = "media",
            streamSid = _aiSpeechAssistantStreamContext.StreamSid,
            media = new { payload = holdOn }
        };

        await SendToWebSocketAsync(twilioWebSocket, holdOnAudio, cancellationToken);

        var systemPrompt = _aiSpeechAssistantStreamContext.Assistant.ModelVoice == "alloy"
            ? "你是一名精通中文的电话录音分析师，你能准确完整地复述客人在需要订购的商品。\n\n以下是在售商品清单\n马蹄片罐头,鸡蛋,红萝卜,薯仔,九层塔,鲜姜,青葱,蒜子肉,蘑菇,虾仁21/25,牛霖,鸡骨,腰果,柠檬水,白胡椒粉,黄瓜,茄子,香茅,青椒,四季豆,芥兰,青江菜,西兰花,芽菜,鱿鱼筒,龙利鱼片,鸡脾肉_无皮,鸡全翼,素春卷,\n白醋,硬豆腐,黄洋葱,红椒,菠萝罐头,蚝油,食用盐,无头鸭,辣椒酱,墨椒,加工类（鸡）,蘑菇片罐头,中锤翼,去皮花生,芫荽,荷兰豆,紫洋葱,金鲳鱼,去肥西冷,玉米笋(切断)罐头,芥花籽油（炸油）,橙,薄荷叶,鸭胸,烧排骨,\n云吞皮,西芹,酱油,轻辣椒粉,椰菜,番茄,黄柠檬,炸蟹角,酱油包,炸粉,月历,笋丝罐头,龙口粉丝,日式面包糠,椰子,青木瓜,味精,鸡胸肉_双边,中青口,白芝麻,竹串,茄汁罐头,胶袋_11X14\n"
            : "You are a telephone recording analyst who is fluent in Chinese and can accurately and completely repeat the items that customers need to order.\n\nThe following is a list of items on sale:\n\negg, carrot, potato, basil, ginger, green onion, peeled garlic, mushroom, p&d shrimp 21/25, beef peeled knuckle, chix bone, cashew nut, lemon juice, white pepper powder, cucumber, eggplant, lemon grass, green bell pepper, green bean, gai lan, shanghai bok choy, broccoli, bean sprout, squid tube, swai fillet, leg mt s/l chicken thigh meat skinless, whole wing, vegetable spring rolls, white vinegar, firm tofu, yellow onion, red bell pepper, canned pineapple, oyster sauce, salt, h/l duck headless duck, chili sauce, serrano pepper, processed food (chicken), canned mushroom slices , party wing , peanut kernel , cilantro , snow pea , red onion , golden pomfret , beef 100vl striploin fat removed , canned baby corn (cut) , canola fry oil , orange , mint leaf , duck breast , medium & light sparerib , won ton wrappers , celery , soy sauce , paprika , cabbage , tomato , lemon , crab rangoon , soy sauce pack , tempura batter mix , calendar , canned bamboo shoot strips , bean thread , japanese style panko , coconut  green papaya  msg  b/l s/l breast mt butterfly chicken breast double-sided,medium,mussel,white sesame seed,skewer,canned ketchup,produce bag";            
        
        var prompt = _aiSpeechAssistantStreamContext.Assistant.ModelVoice == "alloy"
            ? "帮我用中文完整、快速、自然地复述订单："
            : "Help me to repeat the order completely, quickly and naturally in English:";
        
        using var memoryStream = new MemoryStream();
        await using (var writer = new WaveFileWriter(memoryStream, new WaveFormat(8000, 16, channels: 1)))
        {
            lock(_wholeAudioBufferBytes)
            {
                foreach (var audio in _wholeAudioBufferBytes)
                {
                    var index = 0;
                    for (; index < audio.Length; index++)
                    {
                        var t = audio[index];
                        var pcmSample = MuLawDecoder.MuLawToLinearSample(t);
                        writer.WriteSample(pcmSample / 32768f);
                    }
                }
                writer.Write(memoryStream.ToArray(), 0, _wholeAudioBufferBytes.Count);
            }
        }

        var fileContent = memoryStream.ToArray();
        var audioData = BinaryData.FromBytes(fileContent);
        
        _backgroundJobClient.Enqueue<IAttachmentService>(x => x.UploadAttachmentAsync(new UploadAttachmentCommand
        {
            Attachment = new UploadAttachmentDto
            {
                FileContent = fileContent,
                FileName = Guid.NewGuid() + ".wav"
            }
        }, CancellationToken.None));

        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);
        List<ChatMessage> messages =
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage(prompt)
        ];
        
        ChatCompletionOptions options = new()
        {
            ResponseModalities = ChatResponseModalities.Text | ChatResponseModalities.Audio,
            AudioOptions = new ChatAudioOptions(new ChatOutputAudioVoice(_aiSpeechAssistantStreamContext.Assistant.ModelVoice), ChatOutputAudioFormat.Wav)
        };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        
        Log.Information("Analyze record to repeat order: {@completion}", completion);

        var responseAudio = completion.OutputAudio.AudioBytes.ToArray();

        _backgroundJobClient.Enqueue<IAttachmentService>(x => x.UploadAttachmentAsync(new UploadAttachmentCommand
        {
            Attachment = new UploadAttachmentDto
            {
                FileContent = responseAudio,
                FileName = Guid.NewGuid() + ".wav"
            }
        }, CancellationToken.None));
        
        var uLawAudio = await _ffmpegService.ConvertWavToULawAsync(responseAudio, cancellationToken);

        var repeatAudio = new
        {
            @event = "media",
            streamSid = _aiSpeechAssistantStreamContext.StreamSid,
            media = new { payload = uLawAudio }
        };
        
        await SendToWebSocketAsync(twilioWebSocket, repeatAudio, cancellationToken);
        
        _shouldSendBuffToOpenAi = true;
    }
    
    private async Task ProcessUpdateOrderAsync(AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        Log.Information("Ai phone order items: {@items}", jsonDocument.GetProperty("arguments").ToString());
        
        context.OrderItems = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(jsonDocument.GetProperty("arguments").ToString());
        
        var orderItemsJson = JsonConvert.SerializeObject(context.OrderItems).Replace("order_items", "current_order");
        
        var prompt = context.LastPrompt.Replace($"{context.OrderItemsJson}", orderItemsJson);
        
        context.LastPrompt = prompt;
        context.OrderItemsJson = orderItemsJson;
        
        var orderConfirmationMessage = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = "Tell the customer that I have recorded the order for you. Is there anything else you need?"
            }
        };
        
        context.LastMessage = orderConfirmationMessage;
        
        await SendToWebSocketAsync(_openaiClientWebSocket, orderConfirmationMessage, cancellationToken);
        await SendToWebSocketAsync(_openaiClientWebSocket, new { type = "response.create" }, cancellationToken);
    }
    
    private async Task HandleSpeechStartedEventAsync(CancellationToken cancellationToken)
    {
        Log.Information("Handling speech started event.");
        
        if (_aiSpeechAssistantStreamContext.MarkQueue.Count > 0 && _aiSpeechAssistantStreamContext.ResponseStartTimestampTwilio.HasValue)
        {
            var elapsedTime = _aiSpeechAssistantStreamContext.LatestMediaTimestamp - _aiSpeechAssistantStreamContext.ResponseStartTimestampTwilio.Value;
            
            if (_aiSpeechAssistantStreamContext.ShowTimingMath)
                Log.Information($"Calculating elapsed time for truncation: {_aiSpeechAssistantStreamContext.LatestMediaTimestamp} - {_aiSpeechAssistantStreamContext.ResponseStartTimestampTwilio.Value} = {elapsedTime}ms");

            if (!string.IsNullOrEmpty(_aiSpeechAssistantStreamContext.LastAssistantItem))
            {
                if (_aiSpeechAssistantStreamContext.ShowTimingMath)
                    Log.Information($"Truncating item with ID: {_aiSpeechAssistantStreamContext.LastAssistantItem}, Truncated at: {elapsedTime}ms");
                
                var truncateEvent = new
                {
                    type = "conversation.item.truncate",
                    item_id = _aiSpeechAssistantStreamContext.LastAssistantItem,
                    content_index = 0,
                    audio_end_ms = 1
                };
                // await SendToWebSocketAsync(_openaiClientWebSocket, truncateEvent, cancellationToken);
            }

            _aiSpeechAssistantStreamContext.MarkQueue.Clear();
            _aiSpeechAssistantStreamContext.LastAssistantItem = null;
            _aiSpeechAssistantStreamContext.ResponseStartTimestampTwilio = null;
        }
    }

    private async Task SendInitialConversationItem(CancellationToken cancellationToken)
    {
        var initialConversationItem = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "message",
                role = "user",
                content = new[]
                {
                    new
                    {
                        type = "input_text",
                        text = $"Greet the user with: '{_aiSpeechAssistantStreamContext.Knowledge.Greetings}'"
                    }
                }
            }
        };

        await SendToWebSocketAsync(_openaiClientWebSocket, initialConversationItem, cancellationToken);
        await SendToWebSocketAsync(_openaiClientWebSocket, new { type = "response.create" }, cancellationToken);
    }
    
    private async Task SendMark(WebSocket twilioWebSocket, AiSpeechAssistantStreamContextDto context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(context.StreamSid))
        {
            var markEvent = new
            {
                @event = "mark",
                streamSid = context.StreamSid,
                mark = new { name = "responsePart" }
            };
            await SendToWebSocketAsync(twilioWebSocket, markEvent, cancellationToken);
            context.MarkQueue.Enqueue("responsePart");
        }
    }
    
    private async Task SendToWebSocketAsync(WebSocket socket, object message, CancellationToken cancellationToken)
    {
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message))), WebSocketMessageType.Text, true, cancellationToken);
    }
    
    private async Task SendSessionUpdateAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, string prompt, CancellationToken cancellationToken)
    {
        var configs = await InitialSessionConfigAsync(assistant).ConfigureAwait(false);
        
        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                turn_detection = InitialSessionTurnDirection(configs),
                input_audio_format = "g711_ulaw",
                output_audio_format = "g711_ulaw",
                voice = string.IsNullOrEmpty(assistant.ModelVoice) ? "alloy" : assistant.ModelVoice,
                instructions = prompt,
                modalities = new[] { "text", "audio" },
                temperature = 0.8,
                input_audio_transcription = new { model = "whisper-1" },
                tools = configs.Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool).Select(x => x.Config)
            }
        };

        await SendToWebSocketAsync(_openaiClientWebSocket, sessionUpdate, cancellationToken);
    }

    private async Task<List<(AiSpeechAssistantSessionConfigType Type, object Config)>> InitialSessionConfigAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken = default)
    {
        var functions = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);

        return functions.Count == 0 ? [] : functions.Where(x => !string.IsNullOrWhiteSpace(x.Content)).Select(x => (x.Type, JsonConvert.DeserializeObject<object>(x.Content))).ToList();
    }

    private object InitialSessionTurnDirection(List<(AiSpeechAssistantSessionConfigType Type, object Config)> configs)
    {
        var turnDetection = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.TurnDirection);

        return turnDetection.Config ?? new { type = "server_vad", interrupt_response = true, create_response = true };
    } 
}