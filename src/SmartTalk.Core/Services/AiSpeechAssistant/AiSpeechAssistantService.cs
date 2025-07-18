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
using Google.Cloud.Translation.V2;
using SmartTalk.Core.Constants;
using Microsoft.AspNetCore.Http;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Messages.Constants;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.STT;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Core.Settings.Azure;
using Twilio.Rest.Api.V2010.Account;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Core.Settings.WorkWeChat;
using SmartTalk.Core.Settings.ZhiPuAi;
using Task = System.Threading.Tasks.Task;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Events.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Enums.STT;
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
    private readonly AzureSetting _azureSetting;
    private readonly IOpenaiClient _openaiClient;
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly ZhiPuAiSettings _zhiPuAiSettings;
    private readonly IRedisSafeRunner _redisSafeRunner;
    private readonly TranslationClient _translationClient;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly WorkWeChatKeySetting _workWeChatKeySetting;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IInactivityTimerManager _inactivityTimerManager;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    private StringBuilder _openaiEvent;
    private readonly ClientWebSocket _openaiClientWebSocket;
    private AiSpeechAssistantStreamContextDto _aiSpeechAssistantStreamContext;

    public AiSpeechAssistantService(
        IMapper mapper,
        ICurrentUser currentUser,
        AzureSetting azureSetting,
        IOpenaiClient openaiClient,
        OpenAiSettings openAiSettings,
        TwilioSettings twilioSettings,
        ISmartiesClient smartiesClient,
        ZhiPuAiSettings zhiPuAiSettings,
        IRedisSafeRunner redisSafeRunner,
        TranslationClient translationClient,
        IPhoneOrderService phoneOrderService,
        IAgentDataProvider agentDataProvider,
        ISpeechToTextService speechToTextService,
        WorkWeChatKeySetting workWeChatKeySetting,
        ISmartTalkHttpClientFactory httpClientFactory,
        IRestaurantDataProvider restaurantDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        IInactivityTimerManager inactivityTimerManager,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _mapper = mapper;
        _currentUser = currentUser;
        _openaiClient = openaiClient;
        _azureSetting = azureSetting;
        _openAiSettings = openAiSettings;
        _twilioSettings = twilioSettings;
        _smartiesClient = smartiesClient;
        _zhiPuAiSettings = zhiPuAiSettings;
        _redisSafeRunner = redisSafeRunner;
        _agentDataProvider = agentDataProvider;
        _phoneOrderService = phoneOrderService;
        _httpClientFactory = httpClientFactory;
        _translationClient = translationClient;
        _speechToTextService = speechToTextService;
        _workWeChatKeySetting = workWeChatKeySetting;
        _backgroundJobClient = backgroundJobClient;
        _restaurantDataProvider = restaurantDataProvider;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _inactivityTimerManager = inactivityTimerManager;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;

        _openaiEvent = new StringBuilder();
        _openaiClientWebSocket = new ClientWebSocket();
        _aiSpeechAssistantStreamContext = new AiSpeechAssistantStreamContextDto();
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

        var (record, _, _) = await _phoneOrderDataProvider.GetRecordWithAgentAndAssistantAsync(command.CallSid, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get phone order record: {@record}", record);

        record.Url = command.RecordingUrl;
        
        var audioFileRawBytes = await _httpClientFactory.GetAsync<byte[]>(record.Url, cancellationToken).ConfigureAwait(false);

        string transcription = null;
        try
        { 
            transcription = await _speechToTextService.SpeechToTextAsync(
                audioFileRawBytes, fileType: TranscriptionFileType.Wav, responseFormat: TranscriptionResponseFormat.Text, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e.Message.Contains("quota"))
        {
            var alertMessage = $"服务器异常。";
            
            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, _workWeChatKeySetting.Key, alertMessage, mentionedList: new[]{"@all"}, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        
        var detection = await _translationClient.DetectLanguageAsync(transcription, cancellationToken).ConfigureAwait(false);
        
        record.TranscriptionJobId = await _phoneOrderService.CreateSpeechMaticsJobAsync(audioFileRawBytes, Guid.NewGuid().ToString("N") + ".wav", detection.Language, cancellationToken).ConfigureAwait(false);

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
            .Replace("#{customer_phone}", from.StartsWith("+1") ? from[2..] : from)
            .Replace("#{pst_date}", $"{pstTime.Date:yyyy-MM-dd} {pstTime.DayOfWeek}");
        
        Log.Information($"The final prompt: {finalPrompt}");

        return (assistant, knowledge, finalPrompt);
    }
    
    private async Task ConnectOpenAiRealTimeSocketAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, string prompt, CancellationToken cancellationToken)
    {
        ConfigWebSocketRequestHeader(assistant);

        var url = string.IsNullOrEmpty(assistant.ModelUrl) ? AiSpeechAssistantStore.DefaultUrl : assistant.ModelUrl;
        
        await _openaiClientWebSocket.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);

        await SendSessionUpdateAsync(assistant, prompt, cancellationToken).ConfigureAwait(false);
    }

    private void ConfigWebSocketRequestHeader(Domain.AISpeechAssistant.AiSpeechAssistant assistant)
    {
        switch (assistant.ModelProvider)
        {
            case AiSpeechAssistantProvider.OpenAi:
                _openaiClientWebSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");
                _openaiClientWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {_openAiSettings.ApiKey}");
                break;
            case AiSpeechAssistantProvider.ZhiPuAi:
                _openaiClientWebSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");
                _openaiClientWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {_zhiPuAiSettings.ApiKey}");
                break;
            case AiSpeechAssistantProvider.Azure:
                _openaiClientWebSocket.Options.SetRequestHeader("api-key", _azureSetting.ApiKey);
                break;
            default:
                throw new NotSupportedException(nameof(assistant.ModelProvider));
        }
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

                if (result.Count > 0)
                {
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
                            var payload = jsonDocument?.RootElement.GetProperty("media").GetProperty("payload").GetString();
                            var audioAppend = new
                            {
                                type = "input_audio_buffer.append",
                                audio = payload
                            };
                            await SendToWebSocketAsync(_openaiClientWebSocket, audioAppend, cancellationToken);
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
            _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x => x.RecordAiSpeechAssistantCallAsync(_aiSpeechAssistantStreamContext, CancellationToken.None));
            Log.Error("Receive from Twilio error: {@ex}", ex);
        }
    }

    private async Task SendToTwilioAsync(WebSocket twilioWebSocket, CancellationToken cancellationToken)
    {
        Log.Information("Sending to twilio.");
        var buffer = new byte[1024 * 30];
        try
        {
            while (_openaiClientWebSocket.State == WebSocketState.Open)
            {
                var result = await _openaiClientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var value = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Log.Information("ReceiveFromOpenAi result: {result}", value);

                if (result is { Count: > 0 })
                {
                    try
                    {
                        JsonSerializer.Deserialize<JsonDocument>(_openaiEvent.Length > 0 ? _openaiEvent + value : value);
                    }
                    catch (Exception)
                    {
                        _openaiEvent.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        continue;
                    }
                    
                    var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(_openaiEvent.Length > 0 ? _openaiEvent + value : value);
                    
                    _openaiEvent.Clear();

                    Log.Information($"Received event: {jsonDocument?.RootElement.GetProperty("type").GetString()}");
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "error" && jsonDocument.RootElement.TryGetProperty("error", out var error))
                    {
                        Log.Information("Receive openai websocket error" + error.GetProperty("message").GetString());
                        
                    }
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "session.updated")
                        Log.Information("Session updated successfully");

                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.audio.delta" && jsonDocument.RootElement.TryGetProperty("delta", out var delta))
                    {
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
            
                        await SendToWebSocketAsync(twilioWebSocket, clearEvent, cancellationToken);
                        
                        if (!string.IsNullOrEmpty(_aiSpeechAssistantStreamContext.LastAssistantItem))
                        {
                            Log.Information($"Interrupting response with id: {_aiSpeechAssistantStreamContext.LastAssistantItem}");
                            await HandleSpeechStartedEventAsync(cancellationToken);
                        }
                            
                        Log.Information("stop timer...");
                        StopInactivityTimer();
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
                        
                        Log.Information("start timer...");
                        StartInactivityTimer();
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
            Log.Error("WebSocketException: {ex}", ex);
        }
    }
    
    private void StartInactivityTimer()
    {
        _inactivityTimerManager.StartTimer(_aiSpeechAssistantStreamContext.CallSid, TimeSpan.FromMinutes(2), async () =>
        {
            Log.Warning("No activity detected for 2 minutes.");
            await HangupCallAsync(_aiSpeechAssistantStreamContext.CallSid, CancellationToken.None);
        });
    }

    private void StopInactivityTimer()
    {
        _inactivityTimerManager.StopTimer(_aiSpeechAssistantStreamContext.CallSid);
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
    
    private async Task ProcessRepeatOrderAsync(AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
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
        
        await SendToWebSocketAsync(_openaiClientWebSocket, repeatOrderMessage, cancellationToken);
        await SendToWebSocketAsync(_openaiClientWebSocket, new { type = "response.create" }, cancellationToken);
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
        var session = await InitialSessionAsync(assistant, prompt, cancellationToken).ConfigureAwait(false);
        
        var sessionUpdate = new
        {
            type = "session.update",
            session = session
        };

        await SendToWebSocketAsync(_openaiClientWebSocket, sessionUpdate, cancellationToken);
    }

    private async Task<object> InitialSessionAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, string prompt, CancellationToken cancellationToken)
    {
        var configs = await InitialSessionConfigAsync(assistant, cancellationToken).ConfigureAwait(false);
        
        return assistant.ModelProvider switch
        {
            AiSpeechAssistantProvider.OpenAi => new
            {
                turn_detection = InitialSessionParameters(configs, AiSpeechAssistantSessionConfigType.TurnDirection),
                input_audio_format = "g711_ulaw",
                output_audio_format = "g711_ulaw",
                voice = string.IsNullOrEmpty(assistant.ModelVoice) ? "alloy" : assistant.ModelVoice,
                instructions = prompt,
                modalities = new[] { "text", "audio" },
                temperature = 0.8,
                input_audio_transcription = new { model = "whisper-1" },
                input_audio_noise_reduction = InitialSessionParameters(configs, AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction),
                tools = configs.Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool).Select(x => x.Config)
            },
            AiSpeechAssistantProvider.Azure => new
            {
                turn_detection = InitialSessionParameters(configs, AiSpeechAssistantSessionConfigType.TurnDirection),
                input_audio_format = "g711_ulaw",
                output_audio_format = "g711_ulaw",
                voice = string.IsNullOrEmpty(assistant.ModelVoice) ? "alloy" : assistant.ModelVoice,
                instructions = prompt,
                modalities = new[] { "text", "audio" },
                temperature = 0.8,
                input_audio_transcription = new { model = "whisper-1" },
                tools = configs.Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool).Select(x => x.Config)
            },
            _ => throw new NotSupportedException(nameof(assistant.ModelProvider))
        };
    }

    private async Task<List<(AiSpeechAssistantSessionConfigType Type, object Config)>> InitialSessionConfigAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken = default)
    {
        var functions = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdAsync(assistant.Id, assistant.ModelProvider, cancellationToken).ConfigureAwait(false);

        return functions.Count == 0 ? [] : functions.Where(x => !string.IsNullOrWhiteSpace(x.Content)).Select(x => (x.Type, JsonConvert.DeserializeObject<object>(x.Content))).ToList();
    }

    private object InitialSessionParameters(List<(AiSpeechAssistantSessionConfigType Type, object Config)> configs, AiSpeechAssistantSessionConfigType type)
    {
        var config = configs.FirstOrDefault(x => x.Type == type);

        return type switch
        {
            AiSpeechAssistantSessionConfigType.TurnDirection => config.Config ?? new { type = "server_vad" },
            AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction => config.Config,
            _ => throw new NotSupportedException(nameof(type))
        };
    }
}