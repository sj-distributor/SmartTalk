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

    private int _payloadCount ;
    private StringBuilder _audioBuffer;
    private readonly int _bufferThreshold;

    private bool _canBeInterrupt;

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
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider, 
        int bufferThreshold = 5)
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

        _payloadCount = 0;
        _audioBuffer = new StringBuilder();
        _bufferThreshold = _openAiSettings.RealtimeSendBuffLength;

        _canBeInterrupt = true;
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
                            
                            if (media.TryGetProperty("timestamp", out var timestamp) &&
                                int.TryParse(timestamp.GetString(), out var timestampNumber))
                                _aiSpeechAssistantStreamContext.LatestMediaTimestamp = timestampNumber;   
                            else
                                Log.Warning("Missing 'media' or 'timestamp' field in JSON message.");
                            
                            Log.Information("Receive from twilio media event now, and LatestMediaTimestamp: {LatestMediaTimestamp}, and {ResponseStartTimestampTwilio}", _aiSpeechAssistantStreamContext.LatestMediaTimestamp, _aiSpeechAssistantStreamContext.ResponseStartTimestampTwilio);
                            
                            var payload = jsonDocument?.RootElement.GetProperty("media").GetProperty("payload").GetString();
                            if (!string.IsNullOrEmpty(payload))
                            {
                                Log.Information("Appending twilio audio payload: {Payload}", payload);
                                _audioBuffer.Append(payload);
                                _payloadCount++;

                                if (_payloadCount >= _bufferThreshold)
                                {
                                    var audioAppend = new
                                    {
                                        type = "input_audio_buffer.append",
                                        audio = _audioBuffer.ToString()
                                    };
                                    
                                    Log.Information("Sending buffer to openai websocket, the payload is: {AudioAppend}", audioAppend);
                                    await SendToWebSocketAsync(_openaiClientWebSocket, audioAppend, cancellationToken);
                                    _audioBuffer.Clear();
                                    _payloadCount = 0;
                                }
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
        var buffer = new byte[1024 * 30];
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

                        if (_canBeInterrupt)
                        {
                            var clearEvent = new
                            {
                                @event = "clear",
                                streamSid = _aiSpeechAssistantStreamContext.StreamSid
                            };
            
                            await SendToWebSocketAsync(twilioWebSocket, clearEvent, cancellationToken);
                        }
                        
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
        _canBeInterrupt = false;
        
        var holdOn = _aiSpeechAssistantStreamContext.Assistant.ModelVoice == "alloy" ? 
            "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9/f4+Pj4+Pn49/b29fT08/Pz8/Pz8vLy8vLy8vP09PX19vf5+/z+fnx7enl3dnV0cnFwb29ubm5vb29vb29vb29vb29vcHFycnNzdHR0dHRzc3Nzc3R0dHRzc3Nzc3Nzc3N0dXZ3eXp7fX7+/Pv6+Pf29fTy8fDw7+/v7/Dw8PDx8PDw8PDw8PDw8PDx8fHx8fHw8PDw8PHx8fLy8vLy8/Pz8/Pz9PX09fb39/n5+vv7/f39/v9+fn18e3t6enp5enp6ent7e3p6eXh4eHh4eHh4eHh4eHh2d3d2dnZ2dnd3eHh4eXp6enp7e3t7fHx9fHx8fHx7e3t7fHx9fX5+fv////79/fz7+vn5+Pf39vb29vb29vf3+Pn5+vr7/Pz9/v3+/v7//35+fX19fXx7e3p5eHd2dnZ0dHR0dHR0dHR0dXV1dnZ2dnd4d3h4eHl5eXl6e3x9fn7//f37+vn39vX09PPy8fHx8vLz8/T19fb3+Pn6+/v8/Pz8/Pz8/Pz8+/z7+/r6+vr6+fn6+fn5+Pn4+fn5+fn5+fr6+/z9/v5+fn18e3p6enl4eHh4eHh4eHh4eHh4eHd4d3h3dnV1dXR0dHR0dXR1dnZ3dnZ3dnd3d3d4dnZ2dnZ2dnZ2dXZ2d3h4eXt7fH5+/v37+vn49/b29fT09PT09PT09PT09PT08/Ly8fHw8PDw7+/w8PDx8PHz9PX2+Pn6+/z9/v5+fn19fn19fX19fHx8e3t7enp5enp5eXt7e3x9fn7//v5+/35+fXt6eXh2dXRzcnJycXFxcnNzdXV2d3l6e3t8fX19fXx9fH18fXx8fX19fX7//v7+/v7+/v//fn5+fXx8e3t7ent7e3t8fX1+//7+/v38/Pz7/Pz7+/v7+/v6+vr5+fn49/f29fX19PT09PT08/L08/T08/T08/P08/Pz8/Pz8/Pz8/T09fb29/j5+vv8/f5+fXx8e3t6enp5eXl5eHd3d3Z2dXRzcnFxcXBwcHBwcHBwcXFwcHFxcXFxcXFxcXJycnR1dXd4eXp6fHx9fX1+fn5+///+/v79/f39/f39/f7+/v79/v38/Pv6+fj39/b19fX09PX09PT09PTz8/Pz8/P09PX2+Pn6+/z9/n5+fn19fHt8enl5eHh3dnZ1dXV1dHV1dXV1dnV1dnV1dXR1dHR0dHR0dXV1dnZ2dnd2dnZ3d3d4eXp7fX7+/fz7+vn5+fj49/f39/f3+Pj5+fr7/P3+/n59fXx8fHx8fHx8fH19fX5+/v39/Pv7+vr6+fn5+fj4+Pf39/f39/b29/b39vf29vf29vb39/f4+Pj5+vn5+vr5+vn5+fn5+Pn5+Pj4+fn6+vv7/P3+fn18e3p5eHd2dnV1dHR0dHR0dHV1dnd3eHh4eXp6e3t7fHx9fn1+fn7////+/f39/Pz8/Pz7+/z8+/z7/Pz8/Pz+/f7//n5+fX19fX19fX19fX19fHx7e3l5eHd3dnV0dHR0dHV1dnd3eHl6e3t7fHx7e3p6eHh3dnV1dXR1dXZ2d3l6e3x+//79/Pz7+/v7+/v7/Pz8/Pz7+/r5+ff39vX19PP08/Tz9PT09fb29vf3+Pj4+Pj4+Pj3+Pj4+Pf39vX29/j4+Pf39/f4+fr6+/v8/f39/f39/n59e3t6e3t7e3p5d3V0dHNzc3N0c3NycnJzdHR1dnh4eHh3d3d4eXt7fHx8fHx8fHx9fv/+/n5+fn7//v7+/v38/Pz9///+/Pv8/f39/f39/v9+/35+fXt7enp6ent9/vz6+Pb29/j49/j5+vn59/f5+/5+fX19fX59fXx6eHd5enl7fH79+vj29ff5+fr8/f3//vr5+Pb08vP4/f/+fnt7//jz7+/z9Ph6dXhxbG1vcnd9/vz7/nFjYWpvdW9dV2Dn0tHa4NTFzOHKws/k7/BwV0pDR1FSU1ddaWxqZ2ZjXVtbW1xhaHD6+frq5Ofm4uPk4ubq5+rv9PHu+XV4e3Vzffz67uzr493f4d3c3t7i6ujl7Pv8/XZzcHF1bmpre/Z6cHZ4b21sbHJtZm11a2VoaWNhYV1eXlldbmllevf57eno6Obn6+v0cXb5fHf7+nv193T6e2l5+m5sd3RodP5sev9t8O1z+PH+8+73+u3x+e33fPDr9njy6PL6+fju6/355+rr5+Dk6uDi4+Hr6+nyfnR1b2lw93lzenX8/XH8+3H6+mp69W1sfmtk9Pdqb3x8evFqbell5n5X4ndbeGh7dF52cmFwW2PwWF3uWVpzXl3qXWztZ/Nx7etvfNh7auD9/ebm+uvZffHZXd3mX9No7f7z4PR4/ujUVeHhV8lL4MtMy2RWwFtHvV1JxvlW19ZKz8xN3dRY2+Fh3HPw4F3j++pme+RZ0V1a3lxk8+9FxE1a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T0xJR0VEQ0NERERERERFR0lLTU9TWF5odfXp39nTz83MzMzMzc3MzM3Oz9LT1dbX2dve4+bn5+nt9nltZmFeWldUU1VXWlxeYmZqbnN3d3VzdHzz6eDc2djY2trb29rb3N7h5unr7O3u8fj+enRva2hkY2JgX11cXFxdXl9iZGhscXr47+rm49/d3NrZ2dnZ2dra3N3f4eTm6e30/Ht1cG1qZ2VlZGNiYWBhYmNkZGRlaGtucHBwcXZ7/vz8/v/9+vj3+Pj49PHv7e3t7Ovr6urq7Ozu8PT3+/59eHJubGpoZ2VjYWBgX19eXl5fX2BiY2NkZmhqa21wdHl+/Pfy7+7t6+rp6Ofm5+jp6enp6evs7/Hy8/P09fb39/j49/f29fT09PTy8fHx8/Pz8/Dw7+/v7+/u7u7t7ezs7O3t7e3t7e7v7/Hx8/T29/j5+fr8/v99fHx7e3x7e3l4dnV0dHNycW9vbm5tbW1tbW1ubm5ubm5ub29vb29ubm1sbGtramppaWhoaGdoaWlrbG1ucHN3e379+vn39vX08/Pz8/P09PT19PX19PT08/Lx7/Dw7/Dw8PDy8/T29/f4+fr7+/v8+/v6+fn5+fn5+fn6+vv8/Pz9/f39/f7+/v9+fn18e3p5eHZ2dnZ2dnZ2d3h5enp8fHx8fHx8fH1+fX7+/v39/P39/f7+/35+fX18fHt7e3p5eXh3dnV0cnFwb25ubWxsa2tra2pqamppaWlpaWlpaWlpamprbG1tbm9vcXN0dXZ4ent8fX7+/v39/Pz8/Pv7+vn59/b19PTz8fDw8O/v7+/v7u7u7u7u7u7u7u7v7+/v8O/v8PDx8vP09ff3+fr7/P3+/359fX5+///+/f79/fz8/Pz8/f3+/359fHt7e3t7enp7e3x8fH3///7+/f39/f39/v7+/35+fX19fH1+//79/Pv5+Pb19PPy8fHx8fLy8/P09fb39/j5+fr6+fr5+Pj4+Pf4+Pj5+vr7/f1+fnx7enh4d3Z1dXV1dXZ2dnZ3d3d3d3d3d3d3dnZ1dHR0dHR1dXV1dXV2dnZ2dnZ2dXV0dHNzc3N0dHR0dXZ2d3h5enx8fX5+//7//v9+fn5+fn19fHx8e3x7fHx8fX1+fv/+/v/+/v7+/v7/fn59fX18fHx8fX19fX5+fn5+fn5+fn5+fn59fX18fHx8e3x7fHx8fX19fX5+fv9+fn7/fn7/fn5+fn5+fn7//v79/f38/Pv7+/v7+/v6+vr6+vr6+fn5+fn5+Pn4+fj4+Pj4+Pf39/b29fX19fX19fX19fb29vb39/j4+Pj49/f39vb29fT09PT09PT09PT19fX19fX29fb3+Pj5+fr7/P7/fn18e3t6enp5enp6enp5eXh4dnZ0dHRzc3JycnJzdHR0dXV1dXV1dHNzcnJycXJyc3R0dXZ2d3l5eXl4eHd4d3d2dnV2dnd3eHd3d3Z3dnZ1dHNzcnNzc3R0dXZ3d3l5ent7fHt8e3x8fX1+fv/+/fz8/Pz8/f7/fn5+fX19fX3//v79/f39/v7/fn5+fX18e3t8fH19fX18fHx9fX1+fHx7fHx9fn59fHx9fX59fXx8fHx+/v38+vn39vb19PT19fX09PT09PX09fX29vf3+Pn5+fv7+/39/f7+/f39/v39/Pv6+fn5+Pj4+Pn6+vv7/Pz9/Pv7+vr6+vn5+fj59/j39/j39/f29vf4+Pj4+fn5+vr6+/v7+/v7+vr6+fn4+Pj49/n5+vv8/f5+fXx7enl4eHZ2dHR0dHV1dnV2dnd3d3d3eHh5eXp5enp5enl5eHd4d3d3d3d2dnZ2dXV1dHNzc3JycXFxcHBwcnFyc3N0dXZ3d3h5eXp6ent8fHt7e3p7e3p7e3x8fH1+fv/+/f38+/v7+vr6+fr6+/v8/fv7+vn5+fj29vTy8fDv7u7u7u3t7e7u7u/w8fLz8/T09fb19/b29fX08/Pz8/Pz9PX29/j6/P3+/319fHt7e3t7e3x9ff/+/v39/f3+/319e3p6eHd3dXV1dnZ2d3h4d3h4eHh3dnZ1dXRzc3JycXFyc3N0dHR1dnd3eHh4eXl5eXp5eXl5eXl6ent6enp6enp5enl4eXd3d3d4eHl6fHx+/v38+/v6+fn5+fr6+/z8/f7/fv9+/37//v7+/f38/Pz8+/v7/Pz9/f39/v7+//9+fn5+fv///v79/f3+/f39/f38/Pv7+vr6+fn49/f19fTz9PT19vf4+vv8/f3+fn5+fn7//v38+/v7+vv6+/v6+/v7+/z+/n5+fn19fHx8fHx8fX7+/vz8+/v7/f7/fXt5d3V0cnBwb3BwcnN0dXV1dXZ2dnd3d3d3d3d3dnd3eXp8fX7+/f39/f3+/v9+fXt5d3V1c3NycnN0dXV3eHl6fH1+//9+fn5+fX19fHx7e3t8fv78+vf18/Hv7u7s7Ozs7vDz+Pt9eHNvbGpoZ2ZmZ2doaWpsbW5vb3FydXd5fH79+/n28/Du7evr6+vs7e7w8vX5+/z8/Pz8/P1+fHh0cW9ubm5ucHN4ffz6+Pf18e7q5eDc2tjW1NHPzs3Nzc7P0NTZ3ufxe21nYV1ZVFBOTExNTU5PT05NTEpJSEhJTFFbbujYzsrHxcPCwsLCwsLDw8TGyMvP1t3n+G9lXVlVU1NYY/rg1tHQ0tjsWkY6My4sKysrLS4yNz1IWunPxL24s7Curq6ws7i8wMXIy83P0dTW2Nnb3eDp+21hW1ZQTUlGRERGSU1SWWR1+35pWk9JREA+PT0+P0NHS01NTEpISEpPXurPw7y3s7Cura2tra+yt73F0elnWE9LSUhKTlVdZm56+O3t/l1LPjgyLy4uLi80O0dg2cvIz3VHNy4qKSswPmjEt6+trKyrq6qqq6utr7S6v8XL0+BxWk9NTE9XbOLQx8C+v8XVYkY6My8uLzM6R2jSxsTJ3lU/NzEvLi4uLS0tLS4xOENozL22s7O0tbSyr62rqaiqrK+zs7S1usloPzMuLTA8WMu3r6ysrrbGZT4yLCorLztXzr68vsnxSzwyLSspKSkqLC80Oj0+Ozc0NDlFZc/Cvb28u7exrauqqq2wt7u7t7CtrbbUQS8sLjlnv7OvsLO2ubvAz1s/NTI1PVjVxMDE0f5PQToyLisrLTI7RUhANi0pKCouNkNh2cvK097v58/FvLaxr62trq+0u8DEwravq6u8azAoKC1DyrWurbCvsLC0x1Y1LCowRMu0r7G830g6MzMyMzY2Oj4/QEA8OTQtKSQhISUuSsGwrrrzOzAyQdO2rKmqq6urq6yyvM3lxrWqpa/LOCkqM1e/uLq8wrqxra235DwvLz3gtq2ssr3eTEA4NTM0OUJT5/NSQDQvLy4uKiQgHiAqOta4t8BlPDU0O0xqzb60q6ajpKmyxV9g3LqnpKayUi4nJzBVwK+sra2trKqtts1FNDM6crmtqqy32kc5Nzk6NzIxMj1e089dNykhHh4gIygsNUNf4dTuVkQ6OTtCVNS9sKmmpKarsbvAvLOurK/AVzcwMjpIaN/OwrmvqqiprrrUWkxUddjLx8bFxcXHz/BSQjs5Ojw/Pz46NjMzNDY2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dHh5eHp9fPr4/Pjw7vPu7urs7e7u7O70/O75+Xx+dHp8dH5xcnZ5/HxudnpydHtnbWprd2lobG1nbm1jZmX4aHtnX3p57WDobGztbHv1e//1de18cPR+e+rt9fDp5NhZxNRX4fXc49Jn02LWUd3jXMtcXfHqYu7t2VXiynRcz1vYXMhLb1l2T9BbTe1FsFC84fjMbdNFzU9030nL4Wln0UXVft/qeV7EWW3NRMU7wUvQVVDHPkjvYk1WTGRAxjnQZuZMVdNPS2jOQbw41VDNNs/JQNY4rTS3Ra5NSblGwjW9OLEuwn5ZyTu4TXDf3tdBWk/MR9FLxUe9KKk9QNpZz8oxv748VMv4RcdEtM46v7/FQLxLw109q0L3yr5V1s5jszu83uu/ZMP41Ve4QsHGVcPNMbJhP/e92NVUwMBCVsU8a1D4wUDezcY+weXk+E50zV9P7drmPF9dX0tRX8pASdraRT3YwDozzXNWQEzV1S9cxFs+SFnHQz/SUEpc1sxPQnv7RExX81ddTlJOUU/t4GPgU0h60kFK8Fv6SEbA3WHFZmDET0xnOTzPW/rPfsnZVedyTOJZUcReP1HfzlVOvr9ERVv8dvBIx7lq0L3EWU3BxeP4x8DaTM2zy05Yv8JDPb6zQ/a9v9w5y75PabfGwFRCzM1cScXJvXA/3Pd1X2y4s145P1RTO8etun5QPVxSPMqptzc+xdxGOt+vzztJymRBTb7YRP5aVVROybfAQzlfTjne0U67wDlPvEBDXDpIzsxEXMrsa1vsvjwt0MnOy9N5yndeS0DhTtfB3PFwPmW33js81b/ESz/b2n1bV108P8vAS0DGuWctOtO3wTxFtdAsMMCptEhA0lMvPcWrtzkyVNFNOM2w4DM4yL3LRzTXtWc7/kzhVja/skhIXVLC10BcvcdZPFmwuT5Ay0pGvcBVzsprTUdMyrjX7GxaVW9zu7lmPTxfvddEP/awwjc51MDMPki2vfBLVte8zj5a2N5GQtO0sbxLKy5Gwra6ZeFlMDDms7ZYMj/SzklZ0cLtOkX1yuxNUt/fXz4/wrtOQFR3v908YbO/e0xMwLpZQtWurkozXri3zUBPyuJNXr7BW0VC3bpdOVjQzdRHOUxKRcS/PzU9T9ZhOEbM5EtL9rq7Tjxbxr7HY23KvLe7y97Ku77MyMS9usbE31R3XT5I3MXVRjc+XlA/NjA6ST0xMz08MC84U9pNNjE3PGfKvbvIbERRuaipr7i3rrG7t7OutMji1si8ymVe5M7Izb2ytc5VST08T83RdU46Kyw3Rl5CLSw1NzQtKCw5SltCNDNCUUtZ2r+yt+v5vre1tLe1ubqjnaW5X0jSwGFMfcfFzW/UvL3YSD5gwM9RVM+5ulg0MTdIX15QSVJHMywvNTc3ODw/NCYfJTzIvts+Nz1GSe2+raew2FPbtKenrba/vKmen8MrKjnKs8JWU1rPwuXvubbPTDtNwL5pWdXDxVwwLkxzWkxGS2BWOCoqNUdHOjo3LyomJzfDtL5TLi5K18u+u7awvdjHubGvtbmxr7Ozsae2LCUyxK29Q3O0rsBGPcuuttZZbs/MVkT9ubHCQi0uPWPZ61FFPj45MThFPTg4NzYxLSkrO+jN5zwwOmrMz3hVzb3Bvrm6uLy+ubrI5b2mnapGKCAvv62zxWzLtsJsV9CzrsVEa7a0x1N0vLrJRTRA0cXkQThQxdhCNzpFPDUwMTxaRy4jIzjP0T4vNGfIUjc737vSP1G9q6u9+eBkYLytpJ66Ljpn8m47Q7m1zvJOX727zcfeaL69yr22tLbnPET5wbe0tcZHNDdZubG94EQ0MDA3Q0pMSDs3MzAsLDhW09tAND0/PTo6TePUx7+9vVMxOrylobRHT8u8vVMyUtA1ONK6rr0/P2ZbY9S6rau43dm9t73e67qvucnWxrzH3cu9ytXEvr42IipIyMlJP99vLyMmNljfUURUQy4qLjtFTP3Kzkw6O0vf08i2rLPfUGjL+zz2scrvTT/lcTg2UljNwL/Cytt0bGDSt62xwc7IwczQvrK2zta/s7G4xsC/ur1ROjowLmG1rrxANT41KSgvcdw5Nk9GLyopLTdBbci+0V1tbk1AR8q4wL21xkI6QVd+9Mu9wO5IUP5kW2Pdz91W0rC2y8S1srzQybi5vrq0s8Di2dDibmTmxMfTyspePTg+yrNHOGJHKyQpMC8rOn17T1fuUDYxODk6Ts7Mzb+/2lM/PEBNX9W8ur3Gb1lNNzp0xbu1trOxur69vL3AubS6uLO1u77EzNvj9WhQS+PjWuLSPjlaTTxJZ0zKwUdLSjcvLCw9PkDuznxWUUE1MDM2Okf+2t7f2nlHSlc/PUtl18e8t7OwsLK1trzBvLq9vbe1ubu7vsrPwL7CwcTFZULWOiUvOCcrRz5BdczX2dbXWURIRT09Q0hNVOdjW19OQUREOjo4Mzo7M0NcV2HIxs2/vcfFvr3Awbm+wLS4uLKvsbCvuL27wMu+ysrAycXDz9Y9O0AoKS0nJy80N0Dg5Ni+wdHP0FBCQjwzODk2OD9GRF95ZOP77nRMW0pISUdSXG/Tx8W8sbSwra+yrrC3tLK9ure/vru/v72+v7/CwNrd6TU+NikrKScoKi0vOEpM/83M0sfacmhMQj06NjQ2Njg/QkZaZPLT1s/Ly9TLy9bJyMbCvre4ta+yr66usa6xs7W1ur28wMbFx9fe6k9MPDcsLiskKygnLS82O0xscM3J08jP5XhfSUE+OTY2NTQ3PD1CU2n5ysrBvL24urS4t7K3s7OysbKysLSztLW3uLi9vL7Eyc/kX01CNzIsKyUoJyUrKy4zQENY4tnUxsrR1NZlXFNGP0A8Ojw+PUJOT17d1MrAu7m2srOwr6+vrq+vr7KytLe4u76/xsfN0tvwcFFJPzg0LispJikkKSsqMTc/Rmzj1crDysPIzdzbalVVSURIRERJTU9i7tnOwr+7trWxr6+urq6ur6+xtLW4vL7Ays7V7WZcTEQ9OTIvLCkoJyclKiotMTc+SWjf1MTDwb7Aw8fM0ujsYlxYU1haXffq2cvFv7u3tbKvrq+urrGvsbW3uLzBwMvT2fFfVU1BPTkzLy0qKCgnJSgqKjAyOkRScdzKyMS9wr/AxsjL0djd3vDj4eXX1M7Ixb+8ure1tLK0srO2tre7u77BxcnN1d7ubVpNSkE8OTUwLywsKSoqKS0uLzc7Qkxu8dXNxcrFxMrLytLT1dnX2NTPzMnGw8C9vLq6uLa5tre6ubu+v8TFzM/R3u32XltUSUpDPj45ODYyMTAuLy4vMTA2PDpHU1Ho4tXM0sbL0MTO0cbRzcvLxsjCwb+9u765u7q7ur28u76/wsDLy8zc2/d1X1lUTUlLQURBPj88Oz06Ojs7OTs7PjhDPzxJTEdaZ2fo5M/LzsLHyL3Hv7/Iv7/EvsjAwsnFxM7F1MrK4NDQ+djs7nnmXWJ7UWJOWk5LSlZFSUxHTEdNS0tQTEpcR1VrS25VdVtkfOhh49zkbM7k5tHc09DczuzU1tZ5z+To3tXr69Rw3Nzu3G3e3WDY5l7j4FXl9GT/W+NfWN5PaWZfYHVM5FtP/G5N3ErvXFPZWVt7eV7gaf3fcep8+Wvjfu78eOdd2PTw32XZaeHw31/OVuzaeW7hZXVxZNlb8f57fXHn5lzbYexl3GfaVejiY17UZWdp7nZd3+5pZXr4ZOBkXt9rb33jVuTgbn3y3mnbcWrUa2bT+XBp2mTf7O9c3m9y6lfNTN9n20vNVH3nU99oaOjvWNxi7uxW4l5pYuZe52TpW9/8cO/0Udps/eRr42tTw0vZ+/vnd13aX3jgcHLr+fBT1ndh3uD2a3Pb6Hjt1FHfbG7r9Wzn6F9lyU7WZnvmUe3jW3HOQsteWdxgY+tW4/NY0VRc0m5zYObdWn7LSn1v9nhZ3OFWWMhG7Wvk5U/P0E3d1VjwYXzuTtxpcF/eXdhZ3OtjZc9b41bQW2PKTF/LVGbGUO7SUdxofdBgUsJZ+9Xs2XNw1nfqa1jUa03RYWb7fOJl681U7s9E1Vt43V5+xEPgzEnNdUzJVWrERnrSS2dtTeFeWudlb85Q3eFpdnlY11Nb3mpR4F1vbPD1aW/Kcmfv1W5U2dNP39nofXTZ3FDg2l5kfH18aeB9bdzq7Ovb5WLr4V1nb21tUlRqVU9eamBo39lr4OpqXWJjVlTtalrz+/3d7OzTavDe5s3S18LMycLTzcnL1dfS2VVUVEQ+QDw0NDc1MDc3Njg3MTc0Lzk6O0duzMGzpKGem5ubm6Grr8lVRj9ESFbQx762tbW9ycxoOjEvKyAnLiAiLSsoLjlJPTlPSTVG3O9NQ1+ss7WgmZubm52p1V0+KSovOlLOsKuvrKy+z8tFLjE3NzpIYE5KeU0yKSooHx8oKywvR956VNzaSzs6WeG7p6WhnaWdlKrQsc4vIydATzy5pKWprrvsMSouLCw/0cO+uLpILiwmHyAlKDJGz73XQD02JycsMTxnvq+uraijo6mrs7WzuJ2mbcu5LCRA/Hhdu6OjwUs+NikjKEXCvb20tvE+KSAjJycvWcm6y0o/MiYiIy00N/GwsMHOwr7W1rirpqempKSoq6600yy9tigmza6sw2u0s0QzSltGOTNYt8tW8cvdRjQyMCgmOGBadNflTjEnIB8qR+vLxsjQbDk72L22r6mjo6q8w9hMR1ylmaC9tJutIxom4MJVzqCfyisnM143LmC5x089V8PlNjRCRj0sKUHE6D86NS8pJis6W+P+387MydBRQF22pKunsiomOVBcs62enLo3Sb3JXEyzn56xVua8wj0yXK6ps9E/NDMtJi/svc5FPEVELCEkNrWksmNIQzcjHSvWvsXtQz88NsWsuLfBVX1eOzU/z6uotMDYPCgnRse6rK6trsU/NUO/p6Ssubu9zlJGwqintks5XGg4LTXjuNA0LS8zNzk8ec9IODU8VE4yLDA+TUdQzry7z0A6Sde8ury4tbzG1mRNVl1qt6erPSYoP7Kswc6/u7trN0m9r664zM3OZ0xLfb66zmFm2szkQjpEWuf+TUtWXGVWTn7S60s8PEVGQUJMZXFMOzY8VOjY2OZtXVVRYN7MysvS2OBdT1Pjy8O8ur2/v77H0sS6uL/SyLW+Wj46VsHCZ0k9P0c7Nz1P9N9TP0FS+O74899vTUBCasO6x3Nn3tLFVDU+XtTcSTxU4P1KODp1vrzPST1DVGdjYO3S2lY/P1XXys7b1cvM319e2sS/xM3V09Xi9fHczs3X7H7x4N7m6ODb3OtvbHvq5/N9eW5sbnjz+W9nZWltaWRiYWNlZGNhX19fY2VgYGz+83VeXGd38/huaGNq9vN+dW/983Vydnrz8vn4/vjo6fD3++7qfXB0dvb6bWxucHtsZW96fG5fY3j39XhrdPv9eWxpeu7o6PL9eHD/8/f4fPbj4+93aG/w6OfwfP74+n57+vD5c3P/8vJ5b3b99vt1cnv07vZ6dn7y8ft7ffn3e2xnbP3w+m9oam5uamx4+P9qY2h493VmZG74+mtiZXX0fm5scvj3cWxz+u70fHl8+fh4cnn9+XxycnNzb2pqcHz7/nZxb3N8//759PP5//338O3t7vDy9vj07Obl5+3w7+vp6+zp5OXr8/Xu6ent9fTu7fL6/fz59/f29vr/ffrx9P14fPXv8/56/vj4/3l6/vn6/n1+fnt6fvv6/3h0dn77/Hx5enx9enVzefz5/nt++vl+dnR89vP5e3l8/Pv9fXx9/fr6/P79/Pv7/H16eXt+/n57d3V2d3Z2d3d3eX3+/n17e339+vv+//36+Pf3+vv8/n19/vv8/f7+/f98eXl9+fb3/H5+/Pn6/nx9/fr8fHd2eXt7eXZ2d3p7eXZ1eHx+fXp5eXt9fn5+fn7//v79/fz69/b19vf39/Tz8/Pz8PDw8/b49vTy8/b4+fj39/j6+ff29vj5+fj29fb4+fn39vf4+vr49/b3+fn4+Pj5+vr59/f5+/z8+/r6/P3+/v7+/35+fv7+fn59fX1+fX18fHx8e3p5eHh4eHh4d3Z2dnV1dHR0dXV0dHR0dHRzc3NzdHRycnJycnJycnJyc3NzcnN0dHV2d3d4eXl6enp7fHx8fXx8e3t8e3t7e3x7e3p6e3x7e3t8fXx8fHx9fX18fX19fX19fX19fX5+/37//v7+/v7+/v7+/v/+/v///v7//v7+/n7+//9+fXx8fHt7e3p6e3t6e3t7e3x8fHx8fHx8fHx9fX1+fn5+/35+fn5+fn1+fX59fHx8fHt7enp5eXh4dnV2dXRzcnJxcHBwcHBvb29vbm5ubm5ubm5ubm5ub3BwcHBwcnJyc3N1dXV1dXZ3d3h4eHh4eHl6ent7fH1+fn7//fz8/Pz7+/r6+fj5+fn4+Pf39vb29vb29vX19vb39/f3+Pj4+Pn5+fn5+fr6+/r6+fr5+fr6+vr6+vr6+/v8/Pz8/f7+//7+/v7+/v79/v38/Pz8/P39/Pz8/f39/f39/v7+/v79/f38/Pz7+/r6+fn5+Pn4+fn6+/v7/Pz9/f7+/v9+///+/v//fv///35+fX19fHt6enl4eHd2dXRzc3Rzc3N0dHR1dHR0dHV2dnZ1dXV1dXV0dHR1dXV1dHR1dnZ1dXR1dXV1dHR0dHV2dnZ3d3h4eXl6e3x9fX5+fn5+//9+//7+/v7///7+///+/f39/Pz8/Pv7+/v7+/v6+/v7+/v8/P39/Pz9/v39/fz9/f39/f3+/v7+/v9+fX19fHx7enp6e3p6enp7enp5ent7e3p6ent8fHt7fHx9fX1+/v79/Pz7+/r6+fn5+fj49/j39/b29vb29fb19fX19PT09PTz9PPz8/Ly8fHx8fHx8fHx8fLy8/P09fX19fX29vb29vb19fX19vb29vb3+Pj5+fn6+fr6+vv7/P3+/v5+fXt6enp5eXl4eHh4eHh4eHh4d3Z2dXRzc3NycnFxcXFycnJyc3Nzc3JycnJzcnJycXJyc3N0dHR1dnd2d3Z2dnZ2dnZ1dXV2d3d4eXl5eXl4eHh4d3Z2dnd3eHh5ent8fH19fn5+/v7+//7+/fz7+/v6+fj4+Pj4+fn5+vr7+/v7+/v7+/v6+/v7+/v8/Pz8+/v7/Pz7+/r6+vv8/Pv7+vv7/Pz8/Pz8+/r6+/z8+/r6+/z9/f79/fz7+vr6+fr5+vr7+/v6+vn39vb2+Pr8/v9+fn18fHx9fXx7enp8fv9+fHp6fH79+/n5+fr7+/v8/P5+fHp5enz+/Pv8/f79/fz7+vr49/f19fTy8fLz9fX19vX19vb19fX19vf4+Pn5+vv8/Pz9/f5+fXx7enl3dnV0dHR0c3JycHBvb29vb29vb29vb25ubm5ubm9vb29vb29ubm5ubm5ubm5ub29vb29vb29wcHBwcHFxcnJzdHV1dnd4eXp7fH1+//79/Pv6+fj49/b29fX09PPz8/Pz8/Pz8/Pz8vLy8vLy8fLy8vLy8vHx8fDx8fHw8PDw8O/v8PDv7+/w8PDw8PHx8vLz8/T09PX19vb39/f39/f4+Pj5+fn6+vr7/Pz8/f7+/35+fX19fHx8e3t7e3p6enl5eHh4d3d3dnZ1dXV0dHRzdHNzc3Nzc3Rzc3Nzc3Nzc3Nzc3NzdHR0dHV1dXZ2d3d4eHl5enp6e3t7fHx8fX19fX1+fn5+fn5+/37///////////////7+/////v7+/v7+/v79/f39/fz8/Pz8/Pz8/Pz8/P39/f39/f3+/f39/f79/f39/f39/f39/f39/v7+/v///35+fn5+fn5+fn5+fn19fHx7e3p6eXl5eHh3d3d3d3d3d3d4eHh4eHl5eXl6eXl5eXp5enp6enp7e3t7e3t7fHx8fH19fX19fn5+fn5+///+/v79/fz8+/v6+vr6+fj4+Pj4+Pj39/f39vb39/b29vb29/f39/f4+Pj5+fn5+fr6+vv7/Pv7/P39/f3+/v7//35+fn5+fX19fHx8fHt7e3t7enp6eXl5eXh5eHh4d3d4d3d3d3Z2dnZ3dnd3d3d4eHd4eHl4eXl5eXl5eXp6enp6e3t7fHx8fXx9fX19fn5+fn7///7+/v79/f39/Pz8/Pz8/Pz8+/v7+/v6+vr6+fn5+fj4+fn4+Pj4+fn5+Pn5+fn5+vn6+vr7+/v7/Pz8/f39/v7+////fn5+fn5+fX19fX19fX18fHx8fHx7e3t7e3t7enp7enp6enp6enp6enp6ent7e3p7e3t7e3x8fHx8fHx9fH19fX19fX5+fn5+fn5+/37////+/v7+/v7+/f7+/f39/f38/Pz7+/v7+/v7+/v6+vr6+vr6+vv7+vv6+/v7+/v7+/v7+/v8/Pz8/Pz8/Pz9/f39/f39/f79/v7+/v7+//7///9+fn5+fn5+fX19fX19fXx8fHx8e3t7e3t7e3t7ent7e3t7e3p7enp6enp6enp6enp6enp6enp6enp7e3t7e3t7fHt8e3x8fHx8fH19fX5+fn5+///+/v7+/v7+/v39/f3+/f39/f39/f39/f39/f3+/v7+/v7+/v7+//7+//7+/v7+/v7+/v3+/f39/f39/f39/f39/f3+/f39/f39/f39/f39/f39/fz9/f39/f38/Pz8/f38/P39/Pz8/Pz8/Pz8/fz9/f39/f3+/v7+/v7+/v//////fn5+fv9+fn1+fn19fX19fXx8fHx8fHx8fHx8fHx8fHx8fH19fX19fX19fX19fX19fX19fX19fX19fX18fX19fH18fX18fX19fX19fX19fn5+fv///v79/v39/f38/Pz8/Pz8/Pz8/Pz8/Pz7/Pv8/Pz8/Pz8/Pz9/f39/f39/v7+/v///35+fn5+fX19fX18fHx8e3t7e3p6enp6enl6eXl5eXl5eXl5eXl5eXl5eXh4eHh4eHl5eHh5eXl5enp6e3t7fHx8fX1+fv/+/v39/Pz7+/v6+vn4+Pj49/f29vb29fb19fX19PT09PT19PX19fX29vb29vf3+Pj4+Pn5+fn6+vv7+/z8/f39/f7+/v//fn5+fn59fX18fHt7e3p6enp5eXl5eXl5eHl5eXh5eXl4eHh4eHh4eHh5eXl5eXl5eXp6enp6e3p7e3t7e3t7fHx8fH19fX1+fn5+///+/v7+/v39/f39/f38+/v7+/v7+/v7+/v7+/v7+/r6+vr6+vr6+vr6+vr6+vr6+vr6+/v7+/v7+/z8/Pz9/f39/f7+/f7+/v7+/v7+//7//////35+/35+fn5+fn5+fn19fX19fX19fHx8fHx8e3t7e3t7e3t7e3t7ent7e3t7e3t7e3t7e3t7fHx8fHx8fH19fX18fX19fX19fX19fX5+fn5+/35+/////v7+/v7+/v39/f39/f38/Pz8/Pz8/Pz7/Pv7/Pz8+/z8/Pz8/Pz8/P38/P38/f38/f39/f39/f3+/v7+/v7+/v///35+fn5+fn5+fn19fX18fHx8fHt7e3t7e3t7e3t7e3p7e3t7e3t7e3t7e3t7ent7e3t7e3t7e3t7fHx8fHx8fHx8fHx8fHx8fX19fn5+fn7//v7+/v79/f39/f39/Pz9/f38/P39/Pz9/Pz8/f39/f39/f39/v7+/v7+/v7+/v/+//7+/v7+/v7+/v7+/v7//v7+/v7+/v///v7+//7+/v7+/v79/f39/f39/f39/f39/f39/f39/f39/f39/fz8/f39/P39/f39/f7+/v7+/v7+/////////37/fn5+fn5+fX19fX18fHx8fHx8fHx8fHx8fHx8fHx8fHx9fH18fHx9fX19fX19fX19fX19fX19fX19fX19fX19fX19fX19fn5+fn5+fv///v7+/v39/f39/f39/Pz9/Pz8/fz8/fz8/Pz9/Pz8/Pz8/Pz8/f39/f39/v7+/v7+/v7/fn5+fn5+fn59fX19fHx8fHx7e3t7enp6ent6enp6enp6enp6enp6enp6enl6enp6enp6enp6e3t7e3t8fHx9fX19fn5+fv/+/v79/f39/Pz7+/v6+vr5+fn5+Pj4+Pj39/f39/b29/b29vf39/f39/f3+Pj4+Pj5+fn5+fr6+vr7+/v8/Pz9/f39/v7+//9+fn5+fX19fHx8e3t7e3p6enp5enp5eXl5eXl5eXl5eHl4eHh4eXh5eXl5eXl5enp6enp6enp6enp7e3t7e3x8fHx8fX19fX1+fn5+fn5+///+/v7+/v39/f38/f38/f38/Pz8/Pz8/Pz8/Pv7+/v7+/v7+/v8/Pz8/Pz8/Pz8/Pz8/f39/v7+/v7//35+fn5+fn59fX19fXx8fXx8fH19fHx8fHx8fHx8fHx8fHx8fHx8fHx8fH19fX19fX19fX19fX19fn5+fn5+fn5+///+/v7+/v7+/v7+/f39/f39/f39/f39/f39/f39/f39/f39/f39/f39/Pz9/P38/f38/Pz8/Pz8/Pz8/f39/P39/f38/f39/f39/f39/f38/f39/f39/v39/f79/f79/v7+/v7+/v7+/v7///9+//9+fn5+fn59fX19fX19fX18fHx8fHt7e3t7e3t6e3p6enp6enp6enp6enp6eXp6eXp6enp6enp6ent7e3t7e3t7e3t7e3x8fHx9fX19fX19fX1+fn5+fn5+/////v7+/v7+/f39/f39/f39/f38/f39/P38/f38/Pz8/Pz8/Pz8+/v7+/v7/Pv7+/v7+/v7+/v7/Pv8/Pv8/Pz8/Pz8/Pz7/Pz8/Pz8/Pz8/Pz8/Pz8/Pz8/Pz9/P39/f39/f7+/v7+/v7+////fn5+fn5+fn19fX19fX19fX18fHx8fHx8fHx8e3x8fHt7fHx8fHx8fHx8fX19fX19fX19fX19fX19fX19fX19fX19fX19fX19fX19fX19fX19fX19fX19fn5+fn5+///////////+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v////9+fn5+fn5+fn5+fX59fX19fX19fX18fHx8fHx8fHx8fHx8fHx8fHx9fXx8fH19fH19fX19fX19fn5+fn5+fv///v7+/v79/f38/Pz8/Pz7+/v7+/r6+vr6+vr6+vn6+vr6+vr6+fn5+vn5+vr6+vr6+vr6+vr7+/v7+/v7/Pz8/P39/f7+/v////9+fn59fX19fX19fHx8fHt7e3t7enp6enp6enp6enp6enp6enp6enp6enp6enp6ent7e3t7e3x8fHx8fHx8fHx8fX19fX19fn5+fn7///////7//v7+/v7+/v39/fz8/Pz8/Pz8/Pz8/Pz8+/z7+/z8+/v7+/v7+/v7+/v7/Pv8+/z7/Pz8/Pz8/P39/f39/f39/v7+/v7+/v7/////fn5+fn5+fn59fX19fX19fX18fHx8fHx8fHt7fHt7e3t7e3t7ent7e3t7e3t7e3t7e3t8e3t8fHx8fH19fH19fX1+fn5+fn5+fn5+fn5+fn7//////v7+/v7+/f39/f39/fz8/Pz8/Pz8/Pz8/Pz8/Pz8/Pz8/Pz8/Pz8/P39/P39/f39/f39/f3+/v7+//7+////fn7/fn5+fn5+fn5+fn5+fX19fX19fX19fXx9fHx8fHx8fHx8fHx8e3t7e3t7e3t7e3t7e3t7e3t7e3t7e3t7fHx8fHx8fHx9fX19fX19fX1+fX1+fn5+fv/////+/v7+/v7+/v7+/v7+/v7+/v7+/v7+//////////9+fn5+fn5+fn19fX19fX19fH18fH18fHx9fX19fX19fX19fn5+fn5+fv///////////v7+/v7+/v7+/v7+/v7+/////35+fn5+fn5+fn1+fX19fX19fX19fX19fX19fX19fX19fX19fn5+fn5+fv///////v7+/v79/v39/f39/f39/f39/f39/f39/f39/f39/f39/f39/f7+/v79/f39/f39/f39/f39/f39/f39/f7+/f39/f39/f39/f39/f39/v7+/v7+/35+fn59fX19fX18fX19fX19fHx8fHx8fHx8fHx8fXx8fX19fX19fn5+fX7////+/v79/v7+/P5+/fv+//0=" 
            : "UklGRrQ7AABXQVZFZm10IBIAAAAHAAEAQB8AAEAfAAABAAgAAABmYWN0BAAAAGA7AABMSVNUGgAAAElORk9JU0ZUDQAAAExhdmY2MS4xLjEwMAAAZGF0YWA7AAB9fX19fX19fn5+fn5+fn59fX19fX18fXx8fHx8fHx9fX19fn5+fv///////35+fX18fHt7e3t7e3t8fHx8fHt5eHd3eHl6ent7e3x8fHx8fHt6e3t8fX5+fXx6eXl5eXp7e3x+/vv5+Pf3+Pn6+/z8/Pv7+vr6+/z+fXp3dnd4en39/f3+fHl2cnBwcXFyc3Bua2hmZGNiYWFhYmVpcfzu6Obp7vl6eH7z6N7X0M3KycnLz9bf8ndxeuve2tnc5vL38N/QysXFzNxjTUZERklNTUxLS0xNTUhBPTo6PkzzyLu3trrCzexdTUA6NDIzOU7ev7e5vs9cRT48PUJITWXpz8bFx9LwYV72z8C6t7i7v8G/u7Wwrq+3w+RVTFFs2cjCwsXTaEo8NS8tKyosLC83O3TDt6usrbbPUjYwLSouLTE8ROnHube6xGBFOTM4ODs+PT9GX8m1qqalqbPIYkhV2bytqamrr7a5uLi2usTZUj86NzY1My8sKSgqLTtXzbu3t7rDzmlNOzItKSkqLDE8RlRq7+TNx8fD11lBNTE0Q9q0qKKipa22ubivqqeorLTCycrCubazusfsRDw1MzAvLSkpJyovOE39ysG+u8DK+kQ2LSopKiwuMDEzNz1O2cG4trrB0fdi8869ta+vsLO1tbGurKusr7W8w8rLzcvO1OhlTkU+PDk4NzU1MjQzNzg7PkBKT19fXU5GPzw8PUFFR0Q/PDw/SmDczs7Y+mBe9NDBvLu+wsfGwr26ubm8v8bKysnFxcbLz9rh6uni3NzpaFFJRkVGR0NAPDs7PUFFSkhHQkBARU1YX1hPSkpOW27y93JlYWp76OHd3tza1M/NzM3MzcrJx8jKzc3NzczNztHQ1NTY2NXT0NPV3N/k4N/g6XxnXl1eXVpWUVBQU1NTUE9QUFFQT1BRUlBPT1FUV1dXWFtgaG90/fTp5OHf3dzb2dbV1tfW1NXX2dbR0NXd4NvT0dbd4dzY1+Hu7+Th/GVeam9yaWdlYWRtcG5qZmVpbPF5bF9caP19YFdTaXB6cfx5bGv53eXlfvXb2OD18uHX2e/z3dPrZufR0N3yXtzL3FpNfc3aWmvfyMz6S03w0HbeV01F7srMTzxJuLk5Q769PkLTtuE+QOnYZvlvZF/z2t7e/E9UVd7l3FNF+8dIS3TiUEX0RUlBx73Ka97ecMtVRFTNWUxbUUlZ299q3vbe0vB9315ORmh1aPDX3H3Bxsra1FR623B3ZepMVW757nfi7c3P3trXzmnw8+FFQlBOSE5PT2BU89hoZnTt41Xj2NRq29zd1cPNzL25ubmytbi0wbvB9NXPcFPM2UthZUk/PjwyLi8uJiYpKicoLzEzRdjJwq+sqqqnqa+0vsPG2OzLv769r66vqqqtra24y7/LTkhIPzc2MC8qJiUiHiAjIh8mKx5AvWX3pp+yqqKpytDLQTYxPFJK7berq6WgpKmqrcfgbkU8Qz07X9Z1Y2hMOyolIB4cGyAoKCw7UD6kmMetmJ9azK07JS8+LTX1u6uno6Cgpq6yxUk6PTk4P3fIwra1tLrJSTAuJx4bHR8hJjDl1NW/u8nTtaGcL/mYzSo5rjMjeMLO0aqpoqOur7DDPkZRMS5PwvXVq6q2tbXGRDctJyIgJyssLDtZP0DIwlpTW1k6/a6hqS+omW0n6qcfILm+P86jraqmr7jDZjg8ZS8yxbzauKWrs7u7yzszLiclJCg4Oi1O00M+XddDOkFjRCtXqauzx6eczjtbwSQc4/wx1qWqq6Oqs7XLOzU3ODFN4nm3qqinq7y/w04xLy8oJC83N095UlNNOklILS5Lcikny7mufaycqLZiqPIgKU8vIku4wL2joq2qqM1KdjowOzc7adS6p6uyqqq72MtVMSstNC8sRGc+QOlOODpGVEU2LjxDKzPLykS5oqy2qarI/2o/NTMwOUNd17errbCuq7bGxG87O0A+RfrZyrWvr6ystLPCREX6OSMwWD8tNExkNzXdVTEw0kgpS/4pMf1IP8WytKyuq66/5E5EMS42OTdAzb69saqssbG8x25RT0VKSUh4u7jGsaq2wrq75HbB6D1welBIU0c8RTk1MC40ODc4Pjw+TEJHQDs9PdTT3cK3tbu8vMZiYGRAOUVTQkzZx8a7sLa5tLnHyNVNT1lNR2PO2LyvtLeursDLuspO9M5FRd1SSe1sRktIOjk3MzMzMTg7MztGOjs/RDw7dO7+37++wMK8vNrSzHRLXGhHRFzzXOnDwsW9ur2+vsPQ5tv8cOfm1cbGwrm4uri2vL69ytHRaVV+e1/w3ere33lfVEk+Ojs3NDk2Nzw7NTY7MjM/PDhIV09o2tfZzsjK1s7Q827yZk9VXmtg9M/SzsTBx8fD0dvX2ffs0NzczMfOyMLDwcHAw8XFw8nLx8rMzM3N0NvS3Gz26VNdbUtOVEZEUkE8RkU4PEY2OEU7OEZHP0tOSU1RSk5QTlNfV1rp83TazdzRyNLj2Nho8eH9+tje3dHf0dDX2c7R2c7S09TP19DM0cnExsjBxM7Ix9fZzdfu2dDs4dTe5trs8N1kZu9hTltaSUlQSkJJS0ZES0lHSklKQ0dFQ0NFQkFMRkdXV09q6Wh83uD329Xk39bd6Nzb4NrW2NXa2Nrd2dnZ2c/Q1M/N1tLI0dPOzd7Y1eDn59jg7OLae+7d/fDwfXjqZm7va27q6XDr6Op77el9evr6Y2VnYV1fZWRgaGlgX19nZ2V28m155Hhi6/hdbudnYPB0X21+YGFraF1fY2BhW2NiXV1qaV9rcGdlbmRiYmVkaG5tfGxt8nxs8e5z++n+/fV0/vN69O376Orq4dzi3dfe4+Li7uTl5OHf3d7c3dvd3d7c6evi7fz073z48PB++vz/emtraW1qbG5paWpnZmpkaHp2cHZ0a2JlYGBhYG11dm59cmltZ2pscHH+9nt7ffb/evbz8vv+fXxvbXdyb3P58fn27+vt7uvp7PD1+Px6dHz5//v28fX39vHx9e/y7vD08vX5eXt4dWxqbG5wbGpwdXBwcHd4cnR99vf18uzu+ff1+H1++v51cG9vbWppb3Jva212dXBucnBvc3Z7dXl8fXVua2tub29vcHJwcHZ2cG5z/n56d3748PD08O7q7PDz8/d+/334+Pn+e339/Xh3e/nz9ff5+vj5+Pn39fPw7+3u8/Ty7Ozt7e/v7uzt9Pn8/P1+enV4/fHw9vv9/Hx0bGdmaWtraWpvev39//799/X0+Pjz8/Py8PT6/31+/v58d3h9/f58en389/T09/n49vb2+fr59fP09PTy8PP3/n7//fv9//779/f5+/359fLz+f59/fr6+317fvr09ft9e318enl2eHv++/v9fn16e3x9fX1+/Pr5+v57dnV4ent7e/718O3u9Pr9/Pr6+/39+fbx7+/x9vr8/Pv7/Pv7+Pf5/H58fv78+/5+fX3/fn18fH1+/35+ff9+fXx6e33+/f58eXh6fvv5+fz+fn19fn7/////fn59e3p6e3x9/fv7/X57d3Z3eHl6e3x8e3t5eXl6enl5eHh3d3Z1dHV2dnZ0cXBwcHFycnBwcXFwb29vb3B0dnV0dHV3ent8e3p6fHx+fXx7eXh4e3p6enp5eHh5e33++/z9/fz7+ff4+/z69/Xy8fX5/Pz6+ff3+Pn49/b19PT29/b1+Pn7/Pz69/f5+vv8/Pv5+vv6+/z9/f7+//7/fn5+fv/9/f79fnx5eXp8fv7+fn19fX19fnx7e3t8e318fX7+/f5+fHt7fH1+/317enl6e318e3t8fX7+/n59e3d3eHp9fn59fH1+/v39fnp4d3p8fv37+/5+e3p8e31+fXx8fHx+fn59/v79/X18enp9fv5+/v7++/v7fnp2dHl9/Pj8+/z/+318fHl+ffz5+Pf+fnl6/Pv08/l+enZ99u/q6+/09/79/Xv9//fv7Orr7ff6fnz5+/Tz8/H39vv7/fj48O/y7/v6/X339e7v8PZ9/nv78/fu8PXt+vD6ff5v/n7x6+/rfvtydPf55+v1+nBvdnx78v/693z2fnxuaWVldXfr7O/sbXRjYWZgcG/66/bp/nl2Y2tgaGxq+mv//3LufPf9cXFpbmp19XrofXZ9Y3J0cfF7ffp7fXhtaGl4fO3vevduam5gbG939vfz+uvt7OP+/mpcX1xr9+fc4uH+aWBZW19tfuft8+xucGxla3Dt7tvh8+xgZ21ndHB7cOjq8d9ud/ph7vv16/r+a3Nkc3514vHo8GhpWWFn+tzg2Ov2bF9oYvPx6Ovt8fHqd+z++d/k19fd3/huXmtdb/9g7WJrcF1sZm939PxiX05KUE5k5ejeeFpNUU1Xblh5ZVvvY3P8W2BWWFxy7vDdc3rt79DIxb/BxMXLy8zJycbCxb+/wcDKz9fs397Xzc3O1eH8ZVpYWFhVTkY/OzY2MzQ2NTk6Oz9BRkdKSEE/ODU2NDxJY82/u7W0tre6vbu7tq+uq6utsLfBy9jo3+Dr9FhHPjg4O0Niyrqyr7K4wNN+UEQ8ODc7RGPMvbi5vsrrWU5JRD85NS8uLzRG4r2wrq6zvc3uUEZCP0BCQkRCPjw3MS0pJyUnLDZWv6+noqKkqbHBcT84OUHsvK6pqKqtuL7EycLAydhOOTArLTRMv62mo6as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PSAcITa1pK5DOC5U0mQ5Ly5LvLvfPzlWw9JCLis138jA09e6rqmvxWjtwrKvtbq5raersL/Duri62VI8OjMyLiopJCEtRFbVQD1AW+jXUz9DRWvkXUlITuXW+09JR1727u9+18O8vL7Ev7m2s7O0s7OvrrO3ubm4ucDH2GhSQjs2MCwqJygrLC0uLzM8Q05XWWHv3dPU7f7u39zrXldSUVhnevjp0sW/vLu6t7Ovrq6vr6+urrCytLe6vsbSdUs+NzEuKyknJikuLi4vNT1LUVlbY+bT1Nz8afLc3HlXTldo8evk287Cu7e4uLaxrq6ur7Cwr7Cyt7u7u8DO9lVLQTgxLSspKSgoKi0wMTI5P0hQWFts7uPc73j749rS4H5y8NvR1NLIv7q3t7a1s7Cvr7CysrGzt7u+wMLFznpRSUE7NC4sKikoKCgpLDAzMzY/SlRfZvrZ1Nfd6+HX1dTW5/Hm3NTR0srAvLq5t7SzsbCwsLKztLa5vMDEx8vQ5VxNRT45My8tKysqKiosLjE0NTg+R05XXm7p3t3e4dnQzs7O0dHPzcvJx8K+vLq5uLa2tra1tba4u72/w8jO193oelxMQz87ODQwLi4tLS0uLzE1Nzk9Q0tSXWzw3tjV09PPzMvKysrKycnHxcTBvry7urm5ubm5uru8vb/CxsvQ2eV7YFdQSkQ/PTs5NjUzMjIyMjM1Nzo8PkJITVVdafXi3djV09DNzMrJycjHxcPCwb++vby7vLy7u7u9vsHDx8rR2ubvdl5UUE1LR0RCQD49PDs6OTg4ODk6PD9CRUhNVF1j+uDd2dPQz87Mx8bHxMTDw8LBwb++vsHBwsHCw8XIyMvO1+Pg6OhsZlteVktIRkFDQD49PD4/PD0+PkJCP0JGTVVSU1xlaW9m7tnZ2NXSy8vOzsvHxMnLxsLDxMjGxMjLzs/O0Nzf3Nvd8nn//mZcV1VYVE5MSkpLSkpMTE1OTUxNTVFWVVlfZW1lX2388/H359rb3+Pg2NXc3tnS0Nbc2NLS2ODf2Nrk8PDk4/tsam9yaV9gZGJeWVlcW1lZWVxeXFtcXmNjY2lscHd7ffr27+rs7ejm4+Tp49/f4Ofo5efr7e3p6e/5+vj1+Xl5/ntybGlubmtqamxtaGhpaWppaWprbHB1dnVzc3Z3dXl++PT49/Pz9fv+9/Hx9ffz7/H5/fv08vj6+fX2/Xt9/f19d3h7enVwb3J0cG5ucHJwb3BydXRzdHd5eHZ3enx8ent9/318ent8fHp5enp6eHd3eHh3dnZ2dnZ1dnd4d3d2d3d1dHR0dHRycXFycXBvb3Bwb3BxcnJycnN1dnZ3eHp7fHx+fv7+/v39/f39/Pz8/P39/f7+/v7+/v7/fn5+fn5+fn5+fX19fXx8fHx8fHx7fHx8fX19fn7//v79/fz8+/r6+vn5+fj4+Pj4+Pj5+fr6+vr7+/z8/Pz9/f39/v7+/v7+/v7+/v39/fz8/Pv7+vr5+fj49/f39vb29vb29vb29vb29/f3+Pj5+fn6+fr6+/v7/Pz8/P39/f39/f39/f39/Pz7+/v7+vn5+fj49/f29vX19fX09PT09PT09PT09fX19vb39/f4+fn6+vr7+/v7/Pz8/Pz9/f39/f39/fz9/fz8/Pz7+/v7+/r6+vr5+vn5+fj4+Pn4+Pj4+Pj5+fn5+fr6+/v7+/z8/P39/v7//35+fn19fX18fHx8fHx8fHx8fHx8fHx8fHx9fX19fX19fX19fX19fX19fX19fX18fH18fXx8fHx8e3x7e3t7e3p6enp6enp6enp5eXl5eXl5eXl5eXl5eXl5eXl5eXl5eXl5eXl5enp6enp6e3t7fHx8fH19fX19fn5+fn5+fn59fX59fX19fX59fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+//9+/37////+/v7+/v79/f39/f3+/f39/f39/f39/f39/P39/f39/f39/f7+/v7+/v7+///+///+/v7+/v7+/v/+/v7+/v7+/v7+/v7+/v79/v79/f39/v39/f3+/f3+/v7+/v7+/v7+/v7+/v7+/v7+/v/+/v7+/v7+/v7+/v7+/v39/f39/f39/f39/f38/Pz9/Pz8/Pz8/Pz9/f39/fz9/f39/f39/f7+/v///35+fn5+fn5+fn5+fn5+fn5+fX19fX19fX19fn19fX1+fn5+/////v7+/v7+/v39/f39/f39/fz9/Pz8/Pz8/P39/f39/f39/f39/f79/f38/Pz8/Pz8/Pz8/Pz7+/v7+/v7+/v6+vv6+vr6+/v7+/z7/Pz8/Pz8/f39/f39/f3+/v7+/35+fn5+fX5+fX19fX19fX19fX19fX18fX18fX18fHx8fHx8e3x7e3t7e3t7e3t7e3t7e3t7e3t7fHx8fH19fX19fn1+fn19fn19fX19fX19fX19fXx9fHx8fHx8fXx9fX19fX19fX19fn1+fX59fn5+fn5+fn59fX19fX19fX19fX19fX19fX19fXx8fXx9fH18fX18fH18fHx8fHx8fH18fH18fHx9fX19fX19fX1+fX1+fn5+fn5+fn5+//////9+////////////////fv///37//37////////+////////////////fv//fv9+fn5+/35+fn5+fn7/fv///////////////////v///v/+/v////////9+/35+/35+fn5+fn5+fn5+fn5+/35+/////////////v////7//////37///////7////////////+//7//////v7+///+//////////9+//////7+/v///v7+/v7+/v79/fz8/Pz8/fz8/Pz8/Pz8/P39/v7/fn5+fn59fXx8fHx8fX7//v7+//9+fX1+fn19e3p6eXh3dnZ3eHl6eXh1cW9vb3BvbGtra25xcnNxcHR6/n1vZmJiZ21ydn17cmtkX15eXl9jav3j2dTV2uf7fevYzcfFyMnO09XTy8O/v8TK0NTb6GNOTUY9ODImJysrvKqpqVQpJSc5v8DPWzVav6+jq8c4HhwjNbKfoKlfJB8eJ02/qKGjp7JXNywrRMetpaess73MzG9Pd82vo6aovkYwLzdYxDM5JChOXay85TksMDzd0N1FMzA53LKrrMM2JR4iONGtqbJlLSEfKT26raitzUc6PEJoZvPRv6+qrK+6+dXVuq2rqKvBzGxMujo0LyAvOvi5t9PORzhMN0Q+MjU5TsCqp6W3Lh8YGi2+op2laiUZGSVfqqGqxjYuN0zAuLvE8t66saelrbC9w7e0r620urrdRzAgKSYt0O6wq7Ky2jgpHh0kK9yvpqCqwTIeGhwq06egpsE2JyQrPnXW2FVGTn66raysvfNPUMivp6CgqK7EcN7HuL3NTSwkHiQ+Raqkq6VAKh8WHyY3u7KtqL7LRykmIihKvKmirs06JykqLzc0Pk/Fq5+dnafHPCow46yenp+pt9J8vr++Sy4hIBsoyLmUn6O8Hh8WHSxMx7a0ybpSUD8rMSwzYdCysLzyOCckICY3Tq+knpqeo7VqPkJkvamrp6q0r8HBak0vJxwTJSWum56YzjofGh0l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ODczMTNSW05CYMrNytS6u7G1trGxrba2t7a9ycPEydbTyc3X4dnmXlNPSUBDRkRAQkZEQERHRkFDREJBREhGRUdWYmVTUVZdV0xJTllVUFNt8nxu+NvY2tzUzs7P0M7My8rLycjJzM7O0dbd4Obn4+Lf3dfW19jY2d3j6vD8c2xnZWRkY19dXFtaWllZW11dXF1fYmBfYmZmZGVmZWVmZWVmamxvd/ns5uLg397e3t7f3+Dj5eTh4OHg3tzc3d/h4uPo7fH09Pby8O7u7u/w8/p4cG5ta2dlaW5vbm50/vTx9vr6+P16d3Z0cHBvbWttc3NubXR7e3RubnN2cXF2eHZ2eX36/nv+8vH8e3349ft6ffj4eG50+u3w+P/59f/+//52b2xrcnBoYW16eHv8fnBwcfrwfWp16O/3evfm8O/07ujudO/d4O7v9XD+6Ovt5+x0+uba6OncdX1yb3RwfHft6vDl2ulXSFDJvcpPP0jfvsPmSERS08LA6VBX59DbeFtaZe1sUk9h1NDX083SztbvW1Ze+uz6YGzvaWNb7NzXbGJbcF1k3WBoW29WV+Tm2ltYZeXm9lFWV05u3dt2Xl5w+97lcF5b+Vrc4nJbalFl3FxdYW5w7PBVbd107mzs6llZYnLrU1r4zGRMT+nkYdpnWFx2a0roc21paFpS6+nrTGrmZ3Vd7vNlYGfQ4fJ9R3XQU9zjXXL22G5r68zZVN1m8PDw+9Lo5Hlszd7v115W2spj8+pk+9bP8HBZfdTk7d3aUl7b0vRO435wzupcWWfb2Whg2/RX193x8V35cVnXcFNZePB9a2Xa9HztY3L35vRuX1dXbu33+lZecd71a2/x/lfe9e/lbV5fcN3tWHD69mX54entT27q1ullUfrcb3rf8vpbZ+vp2e95bWVj5tJqZ+727+1mZu3s53Dn6PDvaWTjZG3Z4X5z5ejW4mpc2+ZZe/J66eZf2178dfbhVfHd89v1YPnkbfL9WGZ9X/PbfedpWOPrfH1uVnLu+OX8YF3g1Gbp6/DaTmvw/OR0dHf78mZ2cHH36Ov0bezv9d/feVtv+e3wbPrq+WJj2ed2bl36a/Ts+dvpWGzj62r5b2VaXfne5Vlb2tN9YPhyeejPZllt+2x103xcTOroVnPn3fD35/R57d74YF9pcHtrbGX1XuTv5eBbWGra/mxcVubbev9QfXXkynBbXnZr6ddsXF7r2Nzl3V7229X3ZnRp3WZ3bm5maXjd3nZ83PddaX1v7+ft52Bm53LPb0p9+d7sXObhaE1d9O/dbk9g6Ojr7GJja2fp5lxgZvTrcnJmc/TuYvrN4Vhl7fbbzfNr3OTb0tjuZ/ze0v5bXtvY63Rd/Njt0c7Q9FlvV1BVV05NSEhUXW9bSk1YX19WUUlDQ0pLTU1HPz9CRkhKRVBVTW7SxsnLwbi0sLGvra2trq2xtrW0trzF1fhdcVhHQkVGREM/QkpFPjk0NDc+TFlfVEhDR0Q4MDA0OTs+QkJFTWjf3WlNTllZTTwzND1pzs/18bqpoJ+io6Kjo6SkqbTH29bYfk9HR0hNXe16TkA/RUc/Ozo6Oz1KfdfnSj07PDo5PUVLQzgyMzk/SlFPRUNYz8bZSzs2NDg5OTUzRM20r62ppJ+cm52iqqyqq7tPOkH2215GRVzYx8TO/GnbzW47MTZERDYuLjlITEQ8PEdUTjwxLzVGXlJCStvE0VBGXOJbPjc1NC8vLywqLz5ZXNqupKKno5uZnqmsp6q6+FRrXERCSmPjzb68vcDBvcPQa0hLUUo7NkRYPTI9/mAzLjxYRjIwOT88PlDs3dvPxsfM0eFbRDkyLSkmKCsrIyMyT0I1YK2nsa+inaCrp6Cns7m1ssfYz8zL1tbLxsbP48fAwXxT3NV4RUvqeUpEV+ROPkFJRzk3Oz06ODtBRUJKVWps9NTR2XR1a1dHOzQxLy0oKjQxKSo/5z81yay7062iqbSqoqi2r6uutb6yt7jCw7zAwM/OzNHVaFnu/V9PSGntTEhp5VA/Un1EOTlLPzE2Qj85OUZWQ0hi1+Nf08TN8Pn3bkI4PTYuKy4xKysxMTg7PWDvyMm8rLmrrbOkra2sq6u0s6uxuLmytsjDx8jneF56VUlLX1BAWVJOSlNgSVRPWFJKUk5KP0RHPz5APz1GQUpRVXLn3dbUzdje11lRSkA9NzQ1MjAwMTc1OEBFTW3a2cK9vLWzs6y7qKu0qLGprq+tr668urXRy9zwX0hKSUY8QkVCPENKQkVFT1BKS1hPSkhNS0JEQ0hDQklOTFRmbuDo2c7f3dvfYFhVTUc+Pz87PDo9PT0+Rk1DWHRu4eXMwczAvbm5tLCvsKmtraSvqa2vrbu5wMb2VV9DQDo6PTQ3Ojs5OUBEREZMX1RRZPhqWmx4V0xXU1FHRWJPRltk6U5o0+hrX9/vZU1h9U5FT1lDP0tDRkA/S0lCRGRXTVnh69bSyLbFt62uraqpqaqrrKy0s7fAxtTe21BaXE1KRk5GQUdFREA9QEA9PD09Ozw9PD0+RERKUk5s+9fYytLDxHnE1HfnU1VQQUo+RUA+TUNJSE5QSEtYTEFKSkA/PUY7Szs7wEF5w766xa+wsrK0q7S6s7W2xbu6x8bDvcDJwry/xsO/xM7Oy9f2a3VcR0lIPjw8Oz09Oj8/RUlJbXB64NzY3Nno18/y3dDO5erP3PvwavpMRUg9NjRDKSY9LSEsOyktOD4/P3nawsbOsbC/vK+0zcvB015X7VxOWO7IyMK1r7Cvq6yur7G2u77K29l5UU5NWEFG7l5rSmjTY3NZz81B9MzQXkfBxFlfy7rcUb223OO6ud/3u85ETdpSKDpHJyctMSQjMyspLjA8ODxJb9Rb27zI1czE2l/qWUxOSkRW2+TNubi3sq+0ubK3x8a+zlJ00U1H68buZMC5wsi/u8DKz+HM9j5L3FI5P9LxQWbGw+PEt7u7sbO2s6+xvrm3v/BKR1IwKy8tLCYrLSwuLzg7PkZKX2hOT1JRQTk9NzEyOT03QnjUzse6s7S0ury6w99g2thLRmfpc1/LvcTEwLq7vsbNwMdcUWV6T0dfX1/l69zIvsxtysPX/drGz9XKyr67ubi4sLGxsb29wdL+SEc3MD84MDNCVj1DaFNTT0c6NDoyLC4wLzA3PT5NeGzox8be69xyUE5QRkpbTU5w09bcwLm3tbS3urq8yNnfaVJbalpOW/lmY3r35Ovu8nnb3vF5cN3Y2tvt2sS+vsC9uLm1trq4uri+ycjS1t7o3Wx26enZ3+x8XFpQSkM5NjY3NzM0Njg+RUtMUWheVE9KRT87NjAwMTExMjY5RV7dy8W8uLe2vL/CxcjQ3fLz1MzKy8zLysO/yMzQ1trp7GNaYlxp8ejh6NzX19HS19jY2uDe2dTOy8rJxcPBwMDFycrM0NXd7n55cWhgX1tda3fu7XZhWllWUUxEPj0+P0FDQUBDRklMTUtJSUpJSEZAPj08PD0+PkJHTlhr593Uz9DS2N7p7/F4cG1rfebc2tbUz8jCwMLGy8/OzMzMztXc2dPPzMzP09LPzcrJy87Q0tLOy8vLzM3OzMvMztLY3d/f5O53ZltWU1JUV1xgaHJ6+vHu8P5rXVVOSkdFQ0FAPz9AQkNFR0lKS0xNTU1OT1FVWFtdX2JkZmdoamxtbW1sbHP97+jj4N/e3NvZ2NjX1tbX2NnZ2NfW1dTU0tDPzczMzMzNzs/Q0tTV1dXW2Nzf4+Xl5unwfG5ra25wcG5sampudv32+X1xbGprcHFxbGVgXVxbXFxbWVVRT05OTk5NTUxMTE5PUlRVVFRVWFxjbXf9/Pr37+zo5OLh4N/f397e3t3c2tnX1tXU1NTT09LS0tPV1dXV1dXW2Nnb3N3d3d7f4ebq7/T39/X3+3x0b25weP74+P15cW5ub3N2dnRwbm1tb3Bwbmxra21vbmxqaWptcHN4e/348e3t7/X8fv727uro6fD8eHd8+/Ht7O/0+37+9u7s6+zu8vX39vj7/X55dnZ8+/Xw8PL3+/3+/vv5+Pt+enVzdXl9/fr4+v56dnV3en78/X14cm9ucHV5fX15dXFwcHFxcG9ubm1ucXZ4eHZycHBydnh5eXh2c3FxcXR5ffz7/P7/fX1+fn19fv79/Pz7+ff29vX29vb19fb4+v1+fv359fLx8vT29/b29fX3+v57ent9/vz8/P3+/fz7/Pv9fnx8fX5+fXt5eHl8//38+/z9/359fX5+fn5+/v39/n59fHx+/vz7+/z9/v79+/v7+/5+fXx9//7+/v9+//38+/v7/Pz8/P3+fn18e3t9fn5+fn59fn7//359fHt6enp6enl5eXl6e3t7fHx8fHx8fHx7e3t6ent7e3t8e3t7e3t6e3t6enp6eXp6ent7e3t8fX19fn5+fn7////+/v7+/v39/Pz8/Pv7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7/Pz8/Pz8/Pz8+/v8+/z8/Pz8/Pz8/Pz8/Pz9/f39/f39/f7+/v7+/v7//////35+fn59fX59fX19fX19fX19fX18fXx8fHx9fXx8fH19fH19fX19fX19fX19fX1+fn1+fn5+fn5+fn5+fn5+fn5+fn5+fn7/fn5+////////fv///////////////////////////////////35+fn5+fn5+fn5+fn5+fn5+fv9+/37/fn5+////////fv9+fv/////////////////+/v7+/v7+//7//////////////////////////////////37//37///////9+fv///37//////37//35+/35+/////////37/fn5+/35+fv//fn5+fn7///9+fv9+fn5+//9+fn5+fn5+fn5+fn5+fv9+fv//fn5+fv//fn5+/////37//////37///////////9+fv///////////v7//v/////+/////////////////////35+fn7/fn5+/////35+fn7//35+fv//////fn7/fv//fn7/////////////fn5+//9+fv//fn5+////////////fv//fv//////////fv///35+////////////////fn7///////////////9+fv////9+fv///37/fv//fv9+fn7/////////fn7///9+fv//fv//fv///////35+/////////35+fn5+////fv9+fv9+fv///35+fn5+fn7//37//35+//9+fn5+//9+////fn7///////////9+/////35+//////9+////////////fv//fv///37/////fv///////////////35+/37///9+//////////////////9+////fv////9+/35+fv///////37///9+//////////9+////////fv///37///////9+/////////35+/////////37///9+/////////////37/////fn7/////////////////fv////9+fv//fv//////////////fv///////////////////37/////fn7/fn7//////////37///////////9+//9+fv///////37///////////////9+fv///37//35+//9+fn7///9+////////fv//fn5+//9+fv////////////////9+/////35+fn5+fn7//////////37/////fn5+fv///35+//////9+fv//////fv////////9+////////////fn7//35+/37/////////////////////fv////////9+////fv////9+fv//////fv///35+////fv//fv////9+fv//fn7/////fv//////fn7///////////////////9+//9+fv9+/35+//////////////////////9+//9+////////fv9+//9+/////////37///9+////////////fv///////////35+/35+////fn7/////////////fv9+fn5+/37///////9+//////9+fv////////9+fv///37/fv9+fn5+//////9+fv///////////////////35+fn7//37///////////////////////////9+fv///35+////////////fv9+////////////fv//////fv///35+fn5+//////9+fn5+//////9+/////////37/////fv9+/35+//9+fn7///9+/////37///9+fv//fn7//37///////9+fn7/fn7/fn7/////////////////////////////////////////////fv///37///////9+/////35+//////9+/////////37//////////37//35+//9+/////////////////37/////fn7//37/////fv///////////37//37///////////////9+////fv9+fv///37/////////fv//fv9+fn5+////////////////////fv9+fn5+fv///////////////////37///9+fv//fv////////////////9+/////////////////37/fn5+fn5+fv//////fn7//35+/37//35+/37//35+//////////////9+////fn7/fn5+fn5+fv//////////fv///////35+/35+/////35+/35+fv//fn7/////////////////fv//fn7/fn7//35+////fv///35+fv///35+fn5+fn5+//////////9+////////fv//fn7/////////fn7//////35+////////fv////9+fn5+////fv///////////37///////////9+fv///35+fv9+////fn5+/35+//9+//9+fn5+////////fv//////fv///////////37//35+fv9+////////fn5+fn7/fv///37//35+fv//fv///////35+fv9+fv//fv//fn5+fv///35+//////7/fv///v7/fv//fv//////fn5+fn7//37/fn7/fn7///9+fn5+//////7+fn5+fv/+/35+fn7///9+fv9+/n5+//7+/v//fn5+fn5+//9+fv//fn19ff79fn3+/3x8+A==";
       
        var holdOnAudio = new
        {
            @event = "media",
            streamSid = _aiSpeechAssistantStreamContext.StreamSid,
            media = new { payload = holdOn }
        };

        await SendToWebSocketAsync(twilioWebSocket, holdOnAudio, cancellationToken);
        
        var prompt = _aiSpeechAssistantStreamContext.Assistant.ModelVoice == "alloy"
            ? "Help me to repeat the order completely, quickly and naturally in English:"
            : "帮我用中文完整、快速、自然地复述订单：";
        
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);
        List<ChatMessage> messages =
        [
            new UserChatMessage(prompt + jsonDocument.GetProperty("arguments"))
        ];
        
        ChatCompletionOptions options = new()
        {
            ResponseModalities = ChatResponseModalities.Text | ChatResponseModalities.Audio,
            AudioOptions = new ChatAudioOptions(new ChatOutputAudioVoice(_aiSpeechAssistantStreamContext.Assistant.ModelVoice), ChatOutputAudioFormat.Wav)
        };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);

        var uLawAudio = await _ffmpegService.ConvertWavToULawAsync(completion.OutputAudio.AudioBytes.ToArray(), 8000, cancellationToken);

        var repeatAudio = new
        {
            @event = "media",
            streamSid = _aiSpeechAssistantStreamContext.StreamSid,
            media = new { payload = uLawAudio }
        };
        
        await SendToWebSocketAsync(twilioWebSocket, repeatAudio, cancellationToken);

        _canBeInterrupt = true;
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