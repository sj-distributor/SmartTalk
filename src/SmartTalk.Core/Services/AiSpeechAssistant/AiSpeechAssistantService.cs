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
using OpenAI.Chat;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.Agents;
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
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.PhoneOrder;
using JsonSerializer = System.Text.Json.JsonSerializer;
using RecordingResource = Twilio.Rest.Api.V2010.Account.Call.RecordingResource;

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
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly ClientWebSocket _openaiWebSocket;
    private readonly ZhiPuAiSettings _zhiPuAiSettings;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public AiSpeechAssistantService(
        IMapper mapper,
        ICurrentUser currentUser,
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

        _openaiWebSocket = new ClientWebSocket();
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
        
        var context = new AiSpeechAssistantStreamContextDto
        {
            Host = command.Host,
            LastPrompt = prompt,
            HumanContactPhone = humanContact.HumanPhone,
            LastUserInfo = new AiSpeechAssistantUserInfoDto
            {
                PhoneNumber = command.From
            },
            Assistant = _mapper.Map<AiSpeechAssistantDto>(assistant),
            Knowledge = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge)
        };
        
        var receiveFromTwilioTask = ReceiveFromTwilioAsync(command.TwilioWebSocket, _openaiWebSocket, context);
        var sendToTwilioTask = SendToTwilioAsync(command.TwilioWebSocket, _openaiWebSocket, context, cancellationToken);

        try
        {
            await Task.WhenAll(receiveFromTwilioTask, sendToTwilioTask);
        }
        catch (Exception ex)
        {
            Log.Information("Error in one of the tasks: " + ex.Message);
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

    private async Task<(Domain.AISpeechAssistant.AiSpeechAssistant assistant, AiSpeechAssistantKnowledge knowledge, string finalPrompt)> BuildingAiSpeechAssistantKnowledgeBaseAsync(string from, string to, int? assistantId, CancellationToken cancellationToken)
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

    private async Task ConnectOpenAiRealTimeSocketAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, string prompt, CancellationToken cancellationToken)
    {
        _openaiWebSocket.Options.SetRequestHeader("Authorization", GetAuthorizationHeader(assistant));
        _openaiWebSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

        var url = string.IsNullOrEmpty(assistant.ModelUrl) ? AiSpeechAssistantStore.DefaultUrl : assistant.ModelUrl;

        await _openaiWebSocket.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);

        await SendSessionUpdateAsync(_openaiWebSocket, assistant, prompt).ConfigureAwait(false);
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
    
    private async Task ReceiveFromTwilioAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context)
    {
        var buffer = new byte[1024 * 10];
        try
        {
            while (twilioWebSocket.State == WebSocketState.Open)
            {
                var result = await twilioWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                Log.Information("ReceiveFromTwilioAsync result: {result}", Encoding.UTF8.GetString(buffer, 0, result.Count));
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await openAiWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Twilio closed", CancellationToken.None);
                    break;
                }

                if (result.Count > 0)
                {
                    using var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(buffer.AsSpan(0, result.Count));
                    var eventMessage = jsonDocument?.RootElement.GetProperty("event").GetString();
                    
                    switch (eventMessage)
                    {
                        case "connected":
                            break;
                        case "start":
                            context.StreamSid = jsonDocument.RootElement.GetProperty("start").GetProperty("streamSid").GetString();
                            context.CallSid = jsonDocument.RootElement.GetProperty("start").GetProperty("callSid").GetString();
                            context.ResponseStartTimestampTwilio = null;
                            context.LatestMediaTimestamp = 0;
                            context.LastAssistantItem = null;
                            
                            _backgroundJobClient.Enqueue<IMediator>(x=> x.SendAsync(new RecordAiSpeechAssistantCallCommand
                            {
                                CallSid = context.CallSid, Host = context.Host
                            }, CancellationToken.None));
                            break;
                        case "media":
                            var payload = jsonDocument?.RootElement.GetProperty("media").GetProperty("payload").GetString();
                            var audioAppend = new
                            {
                                type = "input_audio_buffer.append",
                                audio = payload
                            };
                            await SendToWebSocketAsync(openAiWebSocket, audioAppend);
                            break;
                        case "stop":
                            _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x => x.RecordAiSpeechAssistantCallAsync(context, CancellationToken.None));
                            break;
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Information($"Receive from Twilio error: {ex.Message}");
        }
    }

    private async Task SendToTwilioAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, CancellationToken cancellationToken)
    {
        Log.Information("Sending to twilio.");
        var buffer = new byte[1024 * 30];
        try
        {
            while (openAiWebSocket.State == WebSocketState.Open)
            {
                var result = await openAiWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result is { Count: > 0 })
                {
                    Log.Information("ReceiveFromOpenAi result: {@result}", JsonConvert.DeserializeObject<object>(Encoding.UTF8.GetString(buffer, 0, result.Count)));
                    
                    var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(buffer.AsSpan(0, result.Count));

                    Log.Information($"Received event: {jsonDocument?.RootElement.GetProperty("type").GetString()}");
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "error" && jsonDocument.RootElement.TryGetProperty("error", out var error))
                        Log.Error("Receive openai websocket error" + error.GetProperty("message").GetString());
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "session.updated")
                        Log.Information("Session updated successfully");

                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.audio.delta" && jsonDocument.RootElement.TryGetProperty("delta", out var delta))
                    {
                        var audioDelta = new
                        {
                            @event = "media",
                            streamSid = context.StreamSid,
                            media = new { payload = delta.GetString() }
                        };

                        await SendToWebSocketAsync(twilioWebSocket, audioDelta);
                        
                        if (context.ResponseStartTimestampTwilio == null)
                        {
                            context.ResponseStartTimestampTwilio = context.LatestMediaTimestamp;
                            if (context.ShowTimingMath)
                            {
                                Log.Information($"Setting start timestamp for new response: {context.ResponseStartTimestampTwilio}ms");
                            }
                        }

                        if (jsonDocument.RootElement.TryGetProperty("item_id", out var itemId))
                        {
                            context.LastAssistantItem = itemId.ToString();
                        }

                        await SendMark(twilioWebSocket, context);
                    }
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "input_audio_buffer.speech_started")
                    {
                        Log.Information("Speech started detected.");
                        if (!string.IsNullOrEmpty(context.LastAssistantItem))
                        {
                            Log.Information($"Interrupting response with id: {context.LastAssistantItem}");
                            await HandleSpeechStartedEventAsync(twilioWebSocket, openAiWebSocket, context);
                        }
                    }

                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "conversation.item.input_audio_transcription.completed")
                    {
                        var response = jsonDocument.RootElement.GetProperty("transcript").ToString();
                        context.ConversationTranscription.Add(new ValueTuple<AiSpeechAssistantSpeaker, string>(AiSpeechAssistantSpeaker.User, response));
                    }
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.audio_transcript.done")
                    {
                        var response = jsonDocument.RootElement.GetProperty("transcript").ToString();
                        context.ConversationTranscription.Add(new ValueTuple<AiSpeechAssistantSpeaker, string>(AiSpeechAssistantSpeaker.Ai, response));
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
                                            await ProcessOrderAsync(openAiWebSocket, context, outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                        
                                        case OpenAiToolConstants.ConfirmCustomerInformation:
                                            await ProcessRecordCustomerInformationAsync(openAiWebSocket, context, outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                        
                                        case OpenAiToolConstants.ConfirmPickupTime:
                                            await ProcessRecordOrderPickupTimeAsync(openAiWebSocket, context, outputElement, cancellationToken).ConfigureAwait(false);
                                            break;

                                        case OpenAiToolConstants.Hangup:
                                            await ProcessHangupAsync(openAiWebSocket, context, outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                        
                                        case OpenAiToolConstants.TransferCall:
                                        case OpenAiToolConstants.HandlePhoneOrderIssues:
                                        case OpenAiToolConstants.HandleThirdPartyDelayedDelivery:
                                        case OpenAiToolConstants.HandleThirdPartyFoodQuality:
                                        case OpenAiToolConstants.HandleThirdPartyUnexpectedIssues:
                                        case OpenAiToolConstants.HandleThirdPartyPickupTimeChange:
                                        case OpenAiToolConstants.HandlePromotionCalls:
                                        case OpenAiToolConstants.CheckOrderStatus:
                                            await ProcessTransferCallAsync(openAiWebSocket, context, outputElement, functionName, cancellationToken).ConfigureAwait(false);
                                            break;
                                    }

                                    break;
                                }
                            }
                        }
                    }

                    if (!context.InitialConversationSent && !string.IsNullOrEmpty(context.Knowledge.Greetings))
                    {
                        await SendInitialConversationItem(openAiWebSocket, context);
                        context.InitialConversationSent = true;
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Error("WebSocketException: {ex}", ex);
        }
    }
    
    private async Task ProcessOrderAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        context.OrderItems = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(jsonDocument.GetProperty("arguments").ToString());
        
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

        context.LastMessage = confirmOrderMessage;
        
        await SendToWebSocketAsync(openAiWebSocket, confirmOrderMessage);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
    }

    private async Task ProcessRecordCustomerInformationAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
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
        
        context.UserInfo = JsonConvert.DeserializeObject<AiSpeechAssistantUserInfoDto>(jsonDocument.GetProperty("arguments").ToString());
        
        await SendToWebSocketAsync(openAiWebSocket, recordSuccess);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
    }

    private async Task ProcessRecordOrderPickupTimeAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
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
        
        context.OrderItems.Comments = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(jsonDocument.GetProperty("arguments").ToString())?.Comments ?? string.Empty;
        
        await SendToWebSocketAsync(openAiWebSocket, recordSuccess);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
    }
        
    private async Task ProcessHangupAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
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
                
        await SendToWebSocketAsync(openAiWebSocket, goodbye);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
        
        _backgroundJobClient.Schedule<IAiSpeechAssistantService>(x => x.HangupCallAsync(context.CallSid, cancellationToken), TimeSpan.FromSeconds(2));
    }
    
    private async Task ProcessTransferCallAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, string functionName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.HumanContactPhone))
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
            
            await SendToWebSocketAsync(openAiWebSocket, nonHumanService);
        }
        else
        {
            var (reply, replySeconds) = MatchTransferCallReply(functionName);
            
            _backgroundJobClient.Schedule<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
            {
                CallSid = context.CallSid,
                HumanPhone = context.HumanContactPhone
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
            
            await SendToWebSocketAsync(openAiWebSocket, transferringHumanService);
        }

        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
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
    
    private async Task ProcessRepeatOrderAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        var repeatOrderMessage = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = $"Repeat the order content to the customer. Here is teh current order:{context.OrderItemsJson}"
            }
        };

        context.LastMessage = repeatOrderMessage;
        
        await SendToWebSocketAsync(openAiWebSocket, repeatOrderMessage);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
    }
    
    private async Task ProcessUpdateOrderAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
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
        
        await SendToWebSocketAsync(openAiWebSocket, orderConfirmationMessage);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
    }
    
    private async Task HandleSpeechStartedEventAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context)
    {
        Log.Information("Handling speech started event.");
        
        if (context.MarkQueue.Count > 0 && context.ResponseStartTimestampTwilio.HasValue)
        {
            var elapsedTime = context.LatestMediaTimestamp - context.ResponseStartTimestampTwilio.Value;
            
            if (context.ShowTimingMath)
                Log.Information($"Calculating elapsed time for truncation: {context.LatestMediaTimestamp} - {context.ResponseStartTimestampTwilio.Value} = {elapsedTime}ms");
            
            var clearEvent = new
            {
                Event = "clear",
                context.StreamSid
            };
            
            await SendToWebSocketAsync(twilioWebSocket, clearEvent);

            context.MarkQueue.Clear();
            context.LastAssistantItem = null;
            context.ResponseStartTimestampTwilio = null;
        }
    }

    private async Task SendInitialConversationItem(WebSocket openaiWebSocket, AiSpeechAssistantStreamContextDto context)
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
                        text = $"Greet the user with: '{context.Knowledge.Greetings}'"
                    }
                }
            }
        };

        await SendToWebSocketAsync(openaiWebSocket, initialConversationItem);
        await SendToWebSocketAsync(openaiWebSocket, new { type = "response.create" });
    }
    
    private async Task SendMark(WebSocket twilioWebSocket, AiSpeechAssistantStreamContextDto context)
    {
        if (!string.IsNullOrEmpty(context.StreamSid))
        {
            var markEvent = new
            {
                @event = "mark",
                streamSid = context.StreamSid,
                mark = new { name = "responsePart" }
            };
            await SendToWebSocketAsync(twilioWebSocket, markEvent);
            context.MarkQueue.Enqueue("responsePart");
        }
    }
    
    private async Task SendToWebSocketAsync(WebSocket socket, object message)
    {
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message))), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    private async Task SendSessionUpdateAsync(WebSocket openAiWebSocket, Domain.AISpeechAssistant.AiSpeechAssistant assistant, string prompt)
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

        await SendToWebSocketAsync(openAiWebSocket, sessionUpdate);
    }

    private async Task<List<(AiSpeechAssistantSessionConfigType Type, object Config)>> InitialSessionConfigAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken = default)
    {
        var functions = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);

        return functions.Count == 0 ? [] : functions.Where(x => !string.IsNullOrWhiteSpace(x.Content)).Select(x => (x.Type, JsonConvert.DeserializeObject<object>(x.Content))).ToList();
    }

    private object InitialSessionTurnDirection(List<(AiSpeechAssistantSessionConfigType Type, object Config)> configs)
    {
        var turnDetection = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.TurnDirection);

        return turnDetection.Config ?? new { type = "server_vad" };
    } 
}