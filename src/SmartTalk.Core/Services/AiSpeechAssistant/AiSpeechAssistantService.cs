using Twilio;
using Serilog;
using System.Text;
using Twilio.TwiML;
using NAudio.Wave;
using NAudio.Codecs;
using Mediator.Net;
using Newtonsoft.Json;
using System.Text.Json;
using Twilio.TwiML.Voice;
using SmartTalk.Core.Ioc;
using Twilio.AspNet.Core;
using SmartTalk.Core.Utils;
using System.Net.WebSockets;
using AutoMapper;
using Google.Cloud.Translation.V2;
using SmartTalk.Core.Constants;
using Microsoft.AspNetCore.Http;
using OpenAI.Chat;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Infrastructure;
using SmartTalk.Messages.Constants;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.SpeechMatics;
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
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Enums.Caching;
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
    private readonly IClock _clock;
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly AzureSetting _azureSetting;
    private readonly ICacheManager _cacheManager;
    private readonly IOpenaiClient _openaiClient;
    private readonly IFfmpegService _ffmpegService;
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly ZhiPuAiSettings _zhiPuAiSettings;
    private readonly IRedisSafeRunner _redisSafeRunner;
    private readonly IPosDataProvider _posDataProvider;
    private readonly TranslationClient _translationClient;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IAttachmentService _attachmentService;
    private readonly ISpeechMaticsService _speechMaticsService;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly WorkWeChatKeySetting _workWeChatKeySetting;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IInactivityTimerManager _inactivityTimerManager;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    private StringBuilder _openaiEvent;
    private bool _shouldSendBuffToOpenAi;
    private readonly List<byte[]> _wholeAudioBufferBytes;
    private readonly ClientWebSocket _openaiClientWebSocket;
    private AiSpeechAssistantStreamContextDto _aiSpeechAssistantStreamContext;

    public AiSpeechAssistantService(
        IClock clock,
        IMapper mapper,
        ICurrentUser currentUser,
        AzureSetting azureSetting,
        ICacheManager cacheManager,
        IOpenaiClient openaiClient,
        IFfmpegService ffmpegService,
        OpenAiSettings openAiSettings,
        TwilioSettings twilioSettings,
        ISmartiesClient smartiesClient,
        ZhiPuAiSettings zhiPuAiSettings,
        IRedisSafeRunner redisSafeRunner,
        IPosDataProvider posDataProvider,
        TranslationClient translationClient,
        IPhoneOrderService phoneOrderService,
        IAgentDataProvider agentDataProvider,
        IAttachmentService attachmentService,
        ISpeechMaticsService speechMaticsService,
        ISpeechToTextService speechToTextService,
        WorkWeChatKeySetting workWeChatKeySetting,
        ISmartTalkHttpClientFactory httpClientFactory,
        IRestaurantDataProvider restaurantDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        IInactivityTimerManager inactivityTimerManager,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _clock = clock;
        _mapper = mapper;
        _currentUser = currentUser;
        _openaiClient = openaiClient;
        _cacheManager = cacheManager;
        _azureSetting = azureSetting;
        _ffmpegService = ffmpegService;
        _openAiSettings = openAiSettings;
        _twilioSettings = twilioSettings;
        _smartiesClient = smartiesClient;
        _zhiPuAiSettings = zhiPuAiSettings;
        _redisSafeRunner = redisSafeRunner;
        _posDataProvider = posDataProvider;
        _agentDataProvider = agentDataProvider;
        _phoneOrderService = phoneOrderService;
        _httpClientFactory = httpClientFactory;
        _translationClient = translationClient;
        _attachmentService = attachmentService;
        _speechMaticsService = speechMaticsService;
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
        
        _wholeAudioBufferBytes = [];
        _shouldSendBuffToOpenAi = true;
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

        var agent = await _agentDataProvider.GetAgentByNumberAsync(command.To, command.AssistantId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get the agent: {@Agent} by {AssistantId} or {DidNumber}", agent, command.AssistantId, command.To);
        
        if (agent == null || agent.IsReceiveCall == false) return new AiSpeechAssistantConnectCloseEvent();
        
        InitAiSpeechAssistantStreamContext(command.Host, command.From);

        await BuildingAiSpeechAssistantKnowledgeBaseAsync(command.From, command.To, command.AssistantId, command.NumberId, cancellationToken).ConfigureAwait(false);
        
        _aiSpeechAssistantStreamContext.HumanContactPhone = _aiSpeechAssistantStreamContext.ShouldForward ? null 
            : (await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantHumanContactByAssistantIdAsync(_aiSpeechAssistantStreamContext.Assistant.Id, cancellationToken).ConfigureAwait(false))?.HumanPhone;
        
        await ConnectOpenAiRealTimeSocketAsync(cancellationToken).ConfigureAwait(false);
        
        var receiveFromTwilioTask = ReceiveFromTwilioAsync(command.TwilioWebSocket, command.IsOutBount, cancellationToken);
        var sendToTwilioTask = SendToTwilioAsync(command.TwilioWebSocket, cancellationToken);

        try
        {
            if (_aiSpeechAssistantStreamContext.ShouldForward)
                await receiveFromTwilioTask;
            else
                await Task.WhenAll(receiveFromTwilioTask, sendToTwilioTask);   
        }
        catch (Exception ex)
        {
            Log.Information("Error in one of the tasks: " + ex.Message);
        }
        
        return new AiSpeechAssistantConnectCloseEvent();
    }

    private void InitAiSpeechAssistantStreamContext(string host, string from)
    {
        _aiSpeechAssistantStreamContext.Host = host;
        _aiSpeechAssistantStreamContext.LastUserInfo = new AiSpeechAssistantUserInfoDto { PhoneNumber = from };
    }

    public async Task RecordAiSpeechAssistantCallAsync(RecordAiSpeechAssistantCallCommand command, CancellationToken cancellationToken)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

        await RetryAsync(async () =>
        {
            await RecordingResource.CreateAsync(
                pathCallSid: command.CallSid,
                recordingStatusCallbackMethod: Twilio.Http.HttpMethod.Post,
                recordingStatusCallback: new Uri($"https://{command.Host}/api/AiSpeechAssistant/recording/callback"));
        }, maxRetryCount: 3, delaySeconds: 1, cancellationToken);
    }

    public async Task ReceivePhoneRecordingStatusCallbackAsync(ReceivePhoneRecordingStatusCallbackCommand command, CancellationToken cancellationToken)
    {
        Log.Information("Handling receive phone record: {@command}", command);

        var (record, agent, _) = await _phoneOrderDataProvider.GetRecordWithAgentAndAssistantAsync(command.CallSid, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get phone order record: {@record}", record);

        record.Url = command.RecordingUrl;
        
        var audioFileRawBytes = await _httpClientFactory.GetAsync<byte[]>(record.Url, cancellationToken).ConfigureAwait(false);
        
        if (agent is { IsSendAudioRecordWechat: true })
        {
            var recordingUrl = record.Url;
            if (record.Url.Contains("twilio"))
            {
                var audio = await _attachmentService.UploadAttachmentAsync(new UploadAttachmentCommand { Attachment = new UploadAttachmentDto { FileName = Guid.NewGuid() + ".wav", FileContent = audioFileRawBytes } }, cancellationToken).ConfigureAwait(false);
            
                Log.Information("Audio uploaded, url: {Url}", audio?.Attachment?.FileUrl);
                
                if (string.IsNullOrEmpty(audio?.Attachment?.FileUrl) || agent.Id == 0) return;
                
                recordingUrl = audio?.Attachment?.FileUrl;
            }
            
            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, agent.WechatRobotKey, $"来电电话：{record.IncomingCallNumber ?? ""}\n\n您有一条新的AI通话录音：\n{recordingUrl}", Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
        }

        var language = string.Empty;
        try
        {
            language = await DetectAudioLanguageAsync(audioFileRawBytes, cancellationToken).ConfigureAwait(false);

            await SendServerRestoreMessageIfNecessaryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            const string alertMessage = "服务器异常。";

            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, _workWeChatKeySetting.Key, alertMessage, mentionedList: new[]{"@all"}, cancellationToken: cancellationToken).ConfigureAwait(false);
            await _cacheManager.GetOrAddAsync("gpt-4o-audio-exception", _ => Task.FromResult(Task.FromResult(alertMessage)), new RedisCachingSetting(RedisServer.System, TimeSpan.FromDays(1)), cancellationToken).ConfigureAwait(false);
        }
        
        record.Language = ConvertLanguageCode(language);
        record.TranscriptionJobId = await _phoneOrderService.CreateSpeechMaticsJobAsync(audioFileRawBytes, Guid.NewGuid().ToString("N") + ".wav", language, cancellationToken).ConfigureAwait(false);

        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private TranscriptionLanguage ConvertLanguageCode(string languageCode)
    {
        return languageCode switch
        {
            "en" => TranscriptionLanguage.English,
            "es" => TranscriptionLanguage.Spanish,
            "ko" => TranscriptionLanguage.Korean,
            _ => TranscriptionLanguage.Chinese
        };
    }
    
    private async Task<string> DetectAudioLanguageAsync(byte[] audioContent, CancellationToken cancellationToken)
    {
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);

        var audioData = BinaryData.FromBytes(audioContent);
        List<ChatMessage> messages =
        [
            new SystemChatMessage("""
                                  You are a professional speech recognition analyst. Based on the audio content, determine the main language used and return only one language code from the following options:
                                  zh-CN: Mandarin (Simplified Chinese)
                                  zh: Cantonese
                                  zh-TW: Taiwanese Chinese (Traditional Chinese)
                                  en: English
                                  es: Spanish
                                  ko: Korean
                                                            
                                  Rules:
                                  1. Carefully analyze the speech content and identify the primary spoken language.
                                  2. If the recording contains noise, background sounds, or non-standard pronunciation, focus on the linguistic features (tone, rhythm, common words) rather than misclassifying it.
                                  3. For English with heavy accents or imperfect pronunciation, still classify as English (en).
                                  4. Only return 'es' (Spanish) if the majority of the recording is clearly and consistently spoken in Spanish. Do NOT classify English with accents or noise as Spanish.
                                  5. If the recording contains mixed languages, return the code of the language that dominates most of the speech.
                                  6. Return only the code without any additional text or explanations.
                                                            
                                  Examples:
                                  If the audio is in Mandarin, even with background noise, return: zh-CN
                                  If the audio is in Cantonese, possibly with some Mandarin words, return: zh
                                  If the audio is in Taiwanese Mandarin (Traditional Chinese), return: zh-TW
                                  If the audio is in English, even with a strong accent or imperfect pronunciation, return: en
                                  If the audio is in English with background noise, return: en
                                  If the audio is predominantly in Spanish, spoken clearly and throughout most of the recording, return: es
                                  If the audio is predominantly in Korean, spoken clearly and throughout most of the recording, return: ko
                                  If the audio has both Mandarin and English but Mandarin is the dominant language, return: zh-CN
                                  If the audio has both Cantonese and English but English dominates, return: en
                                  """),
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage("Please determine the language based on the recording and return the corresponding code.")
        ];

        ChatCompletionOptions options = new() { ResponseModalities = ChatResponseModalities.Text };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        
        Log.Information("Detect the audio language: " + completion.Content.FirstOrDefault()?.Text);
        
        return completion.Content.FirstOrDefault()?.Text ?? "en";
    }

    private async Task SendServerRestoreMessageIfNecessaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var exceptionAlert = await _cacheManager.GetAsync<string>("gpt-4o-audio-exception", new RedisCachingSetting(), cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(exceptionAlert))
            {
                const string restoreMessage = "服务器恢复。";

                await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, _workWeChatKeySetting.Key, restoreMessage, mentionedList: new[]{"@all"}, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            // ignored
        }
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
        if (_aiSpeechAssistantStreamContext.IsTransfer) return;
        
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
        
        await CallResource.UpdateAsync(
            pathSid: callSid,
            status: CallResource.UpdateStatusEnum.Completed
        );
    }
    
    private async Task BuildingAiSpeechAssistantKnowledgeBaseAsync(string from, string to, int? assistantId, int? numberId, CancellationToken cancellationToken)
    {
        var inboundRoute = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantInboundRouteAsync(from, to, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Inbound route: {@inboundRoute}", inboundRoute);

        var (forwardNumber, forwardAssistantId) = DecideDestinationByInboundRoute(inboundRoute);

        Log.Information("Forward number: {@forwardNumber} or Forward assistant id: {forwardAssistantId}", forwardNumber, forwardAssistantId);
        
        if (!string.IsNullOrEmpty(forwardNumber))
        {
            _shouldSendBuffToOpenAi = false;
            _aiSpeechAssistantStreamContext.ShouldForward = true;
            _aiSpeechAssistantStreamContext.ForwardPhoneNumber = forwardNumber;

            return;
        }
        
        var (assistant, knowledge, userProfile) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync(from, to, forwardAssistantId ?? assistantId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Matching Ai speech assistant: {@Assistant}、{@Knowledge}、{@UserProfile}", assistant, knowledge, userProfile);
        
        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");
        
        var finalPrompt = knowledge.Prompt
            .Replace("#{user_profile}", string.IsNullOrEmpty(userProfile?.ProfileJson) ? " " : userProfile.ProfileJson)
            .Replace("#{current_time}", currentTime)
            .Replace("#{customer_phone}", from.StartsWith("+1") ? from[2..] : from)
            .Replace("#{pst_date}", $"{pstTime.Date:yyyy-MM-dd} {pstTime.DayOfWeek}");
        
        if (finalPrompt.Contains("#{customer_items}", StringComparison.OrdinalIgnoreCase))
        {
            var soldToIds = !string.IsNullOrEmpty(assistant.Name) ? assistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();
            Log.Information("SoldToIds to be sent to BuildCustomerItemsStringAsync: {@SoldToIds}", soldToIds);
            
            finalPrompt = finalPrompt.Replace("#{customer_items}", "");
            
            _ = _speechMaticsService.BuildCustomerItemsStringAsync(soldToIds, cancellationToken)
                .ContinueWith(task =>
                {
                    if (task.Status == TaskStatus.RanToCompletion && !string.IsNullOrEmpty(task.Result))
                    {
                        var completedPrompt = _aiSpeechAssistantStreamContext.LastPrompt 
                                              + Environment.NewLine 
                                              + task.Result;

                        Log.Information("Final completed prompt: {Prompt}", completedPrompt);

                        _aiSpeechAssistantStreamContext.LastPrompt = completedPrompt;
                    }
                    else if (task.IsFaulted)
                    {
                        Log.Error(task.Exception, "Failed to build customer items string");
                    }
                }, cancellationToken); 
        }
        
        Log.Information($"The final prompt: {finalPrompt}");

        if (numberId.HasValue)
        {
            var greeting = await _smartiesClient.GetSaleAutoCallNumberAsync(new GetSaleAutoCallNumberRequest(){ Id = numberId.Value }, cancellationToken).ConfigureAwait(false);
            knowledge.Greetings = string.IsNullOrEmpty(greeting.Data.Number.Greeting) ? knowledge.Greetings : greeting.Data.Number.Greeting;
        }
        
        _aiSpeechAssistantStreamContext.LastPrompt = finalPrompt;
        _aiSpeechAssistantStreamContext.Assistant = _mapper.Map<AiSpeechAssistantDto>(assistant);
        _aiSpeechAssistantStreamContext.Knowledge = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge);
    }

    public (string forwardNumber, int? forwardAssistantId) DecideDestinationByInboundRoute(List<AiSpeechAssistantInboundRoute> routes)
    {
        if (routes == null || routes.Count == 0)
            return (null, null);
        
        if (routes.Any(x => x.Emergency))
            routes = routes.Where(x => x.Emergency).ToList();

        foreach (var rule in routes)
        {
            var localNow = ConvertToRuleLocalTime(_clock.Now, rule.TimeZone);

            var days = ParseDays(rule.DayOfWeek) ?? [];
            var dayOk = days.Count == 0 || days.Contains(localNow.DayOfWeek);
            if (!dayOk) continue;

            var timeOk = rule.IsFullDay || IsWithinTimeWindow(localNow.TimeOfDay, rule.StartTime, rule.EndTime);
            if (!timeOk) continue;

            if (!string.IsNullOrWhiteSpace(rule.ForwardNumber))
                return (rule.ForwardNumber, null);

            if (rule.ForwardAssistantId.HasValue)
                return (null, rule.ForwardAssistantId.Value);
        }

        return (null, null);
    }
    
    private static DateTime ConvertToRuleLocalTime(DateTimeOffset utcNow, string? timeZoneId)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return TimeZoneInfo.ConvertTime(utcNow.UtcDateTime, tz);
            }
        }
        catch
        {
            return utcNow.UtcDateTime;
        }
        return utcNow.UtcDateTime;
    }
    
    private static List<DayOfWeek> ParseDays(string dayString)
    {
        if (string.IsNullOrWhiteSpace(dayString)) return [];
        
        var list = new List<DayOfWeek>();
        foreach (var token in dayString.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, out var v) && v is >= 0 and <= 6)
                list.Add((DayOfWeek)v);
        }
        return list;
    }
    
    private static bool IsWithinTimeWindow(TimeSpan localTime, TimeSpan? start, TimeSpan? end)
    {
        var startTime = start ?? TimeSpan.MinValue;
        var endTime = end ?? TimeSpan.MaxValue;

        if (startTime == endTime) return false;

        if (startTime < endTime) return localTime >= startTime && localTime <= endTime;

        return localTime >= startTime || localTime <= endTime;
    }
    
    private async Task ConnectOpenAiRealTimeSocketAsync(CancellationToken cancellationToken)
    {
        if (_aiSpeechAssistantStreamContext.ShouldForward) return;

        ConfigWebSocketRequestHeader(_mapper.Map<Domain.AISpeechAssistant.AiSpeechAssistant>(_aiSpeechAssistantStreamContext.Assistant));
        
        var url = string.IsNullOrEmpty(_aiSpeechAssistantStreamContext.Assistant.ModelUrl)
            ? AiSpeechAssistantStore.DefaultUrl : _aiSpeechAssistantStreamContext.Assistant.ModelUrl;
        
        await _openaiClientWebSocket.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);

        await SendSessionUpdateAsync(cancellationToken).ConfigureAwait(false);
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
    
    private async Task ReceiveFromTwilioAsync(WebSocket twilioWebSocket, bool isOutBount, CancellationToken cancellationToken)
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

                            if (_aiSpeechAssistantStreamContext.ShouldForward)
                                _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
                                {
                                    CallSid = _aiSpeechAssistantStreamContext.CallSid,
                                    HumanPhone = _aiSpeechAssistantStreamContext.ForwardPhoneNumber
                                }, cancellationToken));
                            break;
                        case "media":
                            var media = jsonDocument.RootElement.GetProperty("media");
                            
                            var payload = media.GetProperty("payload").GetString();
                            if (!string.IsNullOrEmpty(payload))
                            {
                                var fromBase64String = Convert.FromBase64String(payload);

                                if (_shouldSendBuffToOpenAi && _aiSpeechAssistantStreamContext.Assistant.ManualRecordWholeAudio)
                                    lock (_wholeAudioBufferBytes)
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
                        case "stop":
                            _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x => x.RecordAiSpeechAssistantCallAsync(_aiSpeechAssistantStreamContext, isOutBount, CancellationToken.None));
                            break;
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x => x.RecordAiSpeechAssistantCallAsync(_aiSpeechAssistantStreamContext, isOutBount, CancellationToken.None));
            Log.Error("Receive from Twilio error: {@ex}", ex);
        }
    }

    private async Task SendToTwilioAsync(WebSocket twilioWebSocket, CancellationToken cancellationToken)
    {
        if (_aiSpeechAssistantStreamContext.ShouldForward) return;
       
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

                        if (_shouldSendBuffToOpenAi) 
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
                                        
                                        case OpenAiToolConstants.RepeatOrder:
                                        case OpenAiToolConstants.SatisfyOrder:
                                            await ProcessRepeatOrderAsync(twilioWebSocket, cancellationToken).ConfigureAwait(false);
                                            break;

                                        case OpenAiToolConstants.Hangup:
                                            await ProcessHangupAsync(outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                        
                                        case OpenAiToolConstants.Refund:
                                        case OpenAiToolConstants.Complaint:
                                        case OpenAiToolConstants.ReturnGoods:
                                        case OpenAiToolConstants.TransferCall:
                                        case OpenAiToolConstants.DeliveryTracking:
                                        case OpenAiToolConstants.LessGoodsDelivered:
                                        case OpenAiToolConstants.RefuseToAcceptGoods:
                                        case OpenAiToolConstants.HandlePromotionCalls:
                                        case OpenAiToolConstants.HandlePhoneOrderIssues:
                                        case OpenAiToolConstants.PickUpGoodsFromTheWarehouse:
                                        case OpenAiToolConstants.HandleThirdPartyFoodQuality:
                                        case OpenAiToolConstants.HandleThirdPartyDelayedDelivery:
                                        case OpenAiToolConstants.HandleThirdPartyUnexpectedIssues:
                                        case OpenAiToolConstants.HandleThirdPartyPickupTimeChange:
                                        case OpenAiToolConstants.DriverDeliveryRelatedCommunication:
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
            _aiSpeechAssistantStreamContext.IsTransfer = true;
            
            var (reply, replySeconds) = MatchTransferCallReply(functionName);
            
            _backgroundJobClient.Schedule<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
            {
                CallSid = _aiSpeechAssistantStreamContext.CallSid,
                HumanPhone = _aiSpeechAssistantStreamContext.HumanContactPhone
            }, cancellationToken), TimeSpan.FromSeconds(replySeconds), HangfireConstants.InternalHostingTransfer);
            
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
            
            // await SendToWebSocketAsync(_openaiClientWebSocket, transferringHumanService, cancellationToken);
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
    
    private async Task ProcessRepeatOrderAsync(WebSocket twilioWebSocket, CancellationToken cancellationToken)
    {
        _shouldSendBuffToOpenAi = false;

        await RandomSendRepeatOrderHoldOnAudioAsync(twilioWebSocket, cancellationToken);

        var responseAudio = await GenerateRepeatOrderAudioAsync(cancellationToken);
        
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

    private async Task RandomSendRepeatOrderHoldOnAudioAsync(WebSocket twilioWebSocket, CancellationToken cancellationToken)
    {
        var assistant = _aiSpeechAssistantStreamContext.Assistant;
        
        Enum.TryParse(assistant.ModelVoice, true, out AiSpeechAssistantVoice voice);
        voice = voice == default ? AiSpeechAssistantVoice.Alloy : voice;

        Enum.TryParse(assistant.ModelLanguage, true, out AiSpeechAssistantMainLanguage language);
        language = language == default ? AiSpeechAssistantMainLanguage.En : language;
        
        var stream = AudioHelper.GetRandomAudioStream(voice, language);

        using var holOnStream = new MemoryStream();
        
        await stream.CopyToAsync(holOnStream, cancellationToken);
        var bytes = holOnStream.ToArray();
        var holdOn = Convert.ToBase64String(bytes);
            
        var holdOnAudio = new
        {
            @event = "media",
            streamSid = _aiSpeechAssistantStreamContext.StreamSid,
            media = new { payload = holdOn }
        };

        await SendToWebSocketAsync(twilioWebSocket, holdOnAudio, cancellationToken);
    }

    private async Task<byte[]> GenerateRepeatOrderAudioAsync(CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();

        await using (var writer = new WaveFileWriter(memoryStream, new WaveFormat(8000, 16, channels: 1)))
        {
            List<byte[]> bufferBytesCopy;

            lock (_wholeAudioBufferBytes)
            {
                bufferBytesCopy = _wholeAudioBufferBytes.ToList();
            }

            foreach (var pcmSample in from audio in bufferBytesCopy from t in audio select MuLawDecoder.MuLawToLinearSample(t))
            {
                writer.WriteSample(pcmSample / 32768f);
            }
        }
        
        var fileContent = memoryStream.ToArray();
        var audioData = BinaryData.FromBytes(fileContent);

        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);
        List<ChatMessage> messages =
        [
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage(_aiSpeechAssistantStreamContext.Assistant.CustomRepeatOrderPrompt)
        ];
        
        ChatCompletionOptions options = new()
        {
            ResponseModalities = ChatResponseModalities.Text | ChatResponseModalities.Audio,
            AudioOptions = new ChatAudioOptions(new ChatOutputAudioVoice(_aiSpeechAssistantStreamContext.Assistant.ModelVoice), ChatOutputAudioFormat.Wav)
        };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        
        Log.Information("Analyze record to repeat order: {@completion}", completion);

        return completion.OutputAudio.AudioBytes.ToArray();
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
    
    private async Task SendSessionUpdateAsync(CancellationToken cancellationToken)
    {
        var session = await InitialSessionAsync(cancellationToken).ConfigureAwait(false);
        
        var sessionUpdate = new
        {
            type = "session.update",
            session = session
        };

        await SendToWebSocketAsync(_openaiClientWebSocket, sessionUpdate, cancellationToken);
    }

    private async Task<object> InitialSessionAsync(CancellationToken cancellationToken)
    {
        var assistant = _mapper.Map<Domain.AISpeechAssistant.AiSpeechAssistant>(_aiSpeechAssistantStreamContext.Assistant);
        var configs = await InitialSessionConfigAsync(assistant, cancellationToken).ConfigureAwait(false);
        
        return assistant.ModelProvider switch
        {
            AiSpeechAssistantProvider.OpenAi => new
            {
                turn_detection = InitialSessionParameters(configs, AiSpeechAssistantSessionConfigType.TurnDirection),
                input_audio_format = "g711_ulaw",
                output_audio_format = "g711_ulaw",
                voice = string.IsNullOrEmpty(assistant.ModelVoice) ? "alloy" : assistant.ModelVoice,
                instructions = _aiSpeechAssistantStreamContext.LastPrompt,
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
                instructions = _aiSpeechAssistantStreamContext.LastPrompt,
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
        var functions = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdsAsync([assistant.Id], assistant.ModelProvider, true, cancellationToken).ConfigureAwait(false);

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
    
    private async Task RetryAsync(
        Func<Task> action,
        int maxRetryCount,
        int delaySeconds,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= maxRetryCount + 1; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (attempt <= maxRetryCount)
            {
                Log.Warning(ex, "重試第 {Attempt} 次失敗，稍後再試…", attempt);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }
    }
}