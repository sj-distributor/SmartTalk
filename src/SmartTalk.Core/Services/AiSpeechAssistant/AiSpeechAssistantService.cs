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
using NAudio.Codecs;
using NAudio.Wave;
using OpenAI.Chat;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.OpenAi;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Infrastructure;
using SmartTalk.Messages.Constants;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.OpenAi;
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
    private readonly ClientWebSocket _openaiClientWebSocket;
    private AiSpeechAssistantStreamContextDto _aiSpeechAssistantStreamContext;

    private bool _shouldSendBuffToOpenAi;
    private readonly List<byte[]> _wholeAudioBufferBytes;
    private OpenAiApiKeyUsageStatus _status;
    private readonly IOpenAiDataProvider _openAiDataProvider;
    
    public AiSpeechAssistantService(
        IClock clock,
        IMapper mapper,
        ICurrentUser currentUser,
        AzureSetting azureSetting,
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
        IOpenAiDataProvider openAiDataProvider,
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
        _openAiDataProvider = openAiDataProvider;
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
        
        var agent = await _agentDataProvider.GetAgentByNumberAsync(command.To, command.AssistantId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get the agent: {@Agent} by {AssistantId} or {DidNumber}", agent, command.AssistantId, command.To);
        
        if (agent == null || agent.IsReceiveCall == false) return new AiSpeechAssistantConnectCloseEvent();
        
        _aiSpeechAssistantStreamContext = new AiSpeechAssistantStreamContextDto
        {
            Host = command.Host,
            LastUserInfo = new AiSpeechAssistantUserInfoDto
            {
                PhoneNumber = command.From
            }
        };

        var (assistant, knowledge, prompt) = await BuildingAiSpeechAssistantKnowledgeBaseAsync(command.From, command.To, command.AssistantId, command.NumberId, cancellationToken).ConfigureAwait(false);
        
        _aiSpeechAssistantStreamContext.HumanContactPhone = _aiSpeechAssistantStreamContext.ShouldForward ? null : (await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantHumanContactByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false))?.HumanPhone;
        
        await ConnectOpenAiRealTimeSocketAsync(assistant, prompt, cancellationToken).ConfigureAwait(false);
        
        var receiveFromTwilioTask = ReceiveFromTwilioAsync(command.TwilioWebSocket, cancellationToken);
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
            Log.Information("Error in one of the tasks {@Ex}", ex);
        }
        
        return new AiSpeechAssistantConnectCloseEvent();
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

        var (record, agent) = await _phoneOrderDataProvider.GetRecordWithAgentAsync(command.CallSid, cancellationToken).ConfigureAwait(false);
        
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
            
            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, agent.WechatRobotKey, $"您有一条新的AI通话录音：\n{recordingUrl}", Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
        }

        var language = string.Empty;
        try
        { 
            language = await DetectAudioLanguageAsync(audioFileRawBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e.Message.Contains("quota"))
        {
            const string alertMessage = "服务器异常。";

            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, _workWeChatKeySetting.Key, alertMessage, mentionedList: new[]{"@all"}, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    public async Task TransferHumanServiceAsync(TransferHumanServiceCommand command, CancellationToken cancellationToken)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
        
        var call = await CallResource.UpdateAsync(
            pathSid: command.CallSid,
            twiml: $"<Response>\n    <Dial>\n      <Number>{command.HumanPhone}</Number>\n    </Dial>\n  </Response>",
            timeLimit: 7200
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

    private async Task<(Domain.AISpeechAssistant.AiSpeechAssistant assistant, AiSpeechAssistantKnowledge knowledge, string finalPrompt)> BuildingAiSpeechAssistantKnowledgeBaseAsync(string from, string to, int? assistantId, int? numberId, CancellationToken cancellationToken)
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

            return (null, null, null);
        }
        
        var (assistant, knowledge, userProfile) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync(from, to, forwardAssistantId ?? assistantId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Matching Ai speech assistant: {@Assistant}、{@Knowledge}、{@UserProfile}", assistant, knowledge, userProfile);

        if (assistant == null || knowledge == null || string.IsNullOrEmpty(knowledge.Prompt)) return (assistant, knowledge, string.Empty);

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
            
            var customerItemsString = await _speechMaticsService.BuildCustomerItemsStringAsync(soldToIds, cancellationToken).ConfigureAwait(false);

            finalPrompt = finalPrompt.Replace("#{customer_items}", customerItemsString ?? "");
        }
        
        Log.Information($"The final prompt: {finalPrompt}");

        if (numberId.HasValue)
        {
            var greeting = await _smartiesClient.GetSaleAutoCallNumberAsync(new GetSaleAutoCallNumberRequest(){ Id = numberId.Value }, cancellationToken).ConfigureAwait(false);
            knowledge.Greetings = string.IsNullOrEmpty(greeting.Data.Number.Greeting) ? knowledge.Greetings : greeting.Data.Number.Greeting;
        }
        
        _aiSpeechAssistantStreamContext.Assistant = _mapper.Map<AiSpeechAssistantDto>(assistant);
        _aiSpeechAssistantStreamContext.Knowledge = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge);
        _aiSpeechAssistantStreamContext.LastPrompt = finalPrompt;
        return (assistant, knowledge, finalPrompt);
    }

    public (string forwardNumber, int? forwardAssistantId) DecideDestinationByInboundRoute(List<AiSpeechAssistantInboundRoute> routes)
    {
        if (routes == null || routes.Count == 0)
            return (null, null);

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
            if (int.TryParse(token, out var v) && v >= 0 && v <= 6)
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
    
    private async Task ConnectOpenAiRealTimeSocketAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistant, string prompt, CancellationToken cancellationToken)
    {
        if (_aiSpeechAssistantStreamContext.ShouldForward) return;
        
        await ConfigAuthorizationHeader(assistant, cancellationToken).ConfigureAwait(false);
        
        var url = string.IsNullOrEmpty(assistant.ModelUrl) ? AiSpeechAssistantStore.DefaultUrl : assistant.ModelUrl;
        
        try
        {
            await _openaiClientWebSocket.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await ReduceOpenAiApiKeyUsingNumberAsync(cancellationToken);
        
            throw;
        }

        await SendSessionUpdateAsync(assistant, prompt, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task ConfigAuthorizationHeader(Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken)
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
    
    private async Task ReduceOpenAiApiKeyUsingNumberAsync(CancellationToken cancellationToken)
    {
        var statusList = await _openAiDataProvider.GetOpenAiApiKeyUsageStatusAsync(id: _status.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        var status = statusList.First();
        
        status.UsingNumber -= 1;
        
        await _openAiDataProvider.UpdateOpenAiApiKeyUsageStatusAsync(status, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetIdleOpenAiApiKeyAsync(CancellationToken cancellationToken)
    {
        var apiKeys = _openAiSettings.RealTimeApiKeys;

        var statusList = await _openAiDataProvider.GetOpenAiApiKeyUsageStatusAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await CheckStatusCountEnoughOrAddingAsync(cancellationToken, statusList, apiKeys);
        
        var minUsingNumberStatus = statusList.Take(apiKeys.Count).MinBy(x => x.UsingNumber);

        minUsingNumberStatus.UsingNumber += 1;

        _status = minUsingNumberStatus;

        await _openAiDataProvider.UpdateOpenAiApiKeyUsageStatusAsync(minUsingNumberStatus, cancellationToken).ConfigureAwait(false);

        return apiKeys[minUsingNumberStatus.Index];
    }

    private async Task CheckStatusCountEnoughOrAddingAsync(CancellationToken cancellationToken, List<OpenAiApiKeyUsageStatus> statusList, List<string> apiKeys)
    {
        if (statusList.Count < apiKeys.Count)
        {
            var newStatusList = new List<OpenAiApiKeyUsageStatus>();
            var number = apiKeys.Count - statusList.Count;
            var LastIndex = statusList.MaxBy(x => x.Index)?.Index ?? 0;

            for (var i = 0; i < number; i++)
            {
                newStatusList.Add(new OpenAiApiKeyUsageStatus { Index = LastIndex, UsingNumber = 0 });

                LastIndex++;
            }
            
            await _openAiDataProvider.AddOpenAiApiKeyUsageStatusAsync(newStatusList, cancellationToken).ConfigureAwait(false);
            
            statusList.AddRange(newStatusList);
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

                if (result is { Count: > 0 })
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

                            Log.Information("Should forward: {ShouldForward}", _aiSpeechAssistantStreamContext.ShouldForward);
                            if (_aiSpeechAssistantStreamContext.ShouldForward)
                                _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
                                {
                                    CallSid = _aiSpeechAssistantStreamContext.CallSid,
                                    HumanPhone = _aiSpeechAssistantStreamContext.ForwardPhoneNumber
                                }, cancellationToken));
                            break;
                        case "media":
                            if (_aiSpeechAssistantStreamContext.ShouldForward) break;
                            
                            var media = jsonDocument.RootElement.GetProperty("media");
                            
                            var payload = media.GetProperty("payload").GetString();
                            if (!string.IsNullOrEmpty(payload))
                            {
                                var fromBase64String = Convert.FromBase64String(payload);

                                if (_shouldSendBuffToOpenAi)
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
                            
                            await ReduceOpenAiApiKeyUsingNumberAsync(cancellationToken).ConfigureAwait(false);
                            
                            Log.Information("Session Transcription: {@transcription}", _aiSpeechAssistantStreamContext.ConversationTranscription);
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
        var buffer = new byte[1024 * 40];

        try
        {
            while (_openaiClientWebSocket.State == WebSocketState.Open)
            {
                var result = await _openaiClientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var value = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Log.Information("ReceiveFromOpenAi result: {result}", value);

                if (result is { Count: > 0 })
                {
                    // try
                    // {
                    //     JsonSerializer.Deserialize<JsonDocument>(_openaiEvent.Length > 0 ? _openaiEvent + value : value);
                    // }
                    // catch (Exception)
                    // {
                    //     _openaiEvent.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    //     continue;
                    // }
                    
                    var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(value);
                    
                    // _openaiEvent.Clear();
                    
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
                                        
                                        case OpenAiToolConstants.AddItem:
                                            await ProcessAddNewItemsToOrderAsync(outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                        
                                        case OpenAiToolConstants.RepeatOrder:
                                        case OpenAiToolConstants.SatisfyOrder:
                                            await ProcessRepeatOrderAsync(twilioWebSocket, outputElement, cancellationToken).ConfigureAwait(false);
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
            Log.Information($"Send to Twilio error: {ex.Message}");
        }
    }
    
    private void StartInactivityTimer()
    {
        if (string.IsNullOrEmpty(_aiSpeechAssistantStreamContext.CallSid))
        {
            Log.Information("Starting inactivity timer interrupt, due to the callsid is invalid: {CallSid}", _aiSpeechAssistantStreamContext.CallSid);
            return;
        }
        
        _inactivityTimerManager.StartTimer(_aiSpeechAssistantStreamContext.CallSid, TimeSpan.FromMinutes(2), async () =>
        {
            Log.Warning("No activity detected for 2 minutes.");
            
            await HangupCallAsync(_aiSpeechAssistantStreamContext.CallSid, CancellationToken.None);
        });
    }

    private void StopInactivityTimer()
    {
        if (string.IsNullOrEmpty(_aiSpeechAssistantStreamContext.CallSid))
        {
            Log.Information("Stop inactivity timer interrupt, due to the callsid is invalid: {CallSid}", _aiSpeechAssistantStreamContext.CallSid);
            return;
        }
        
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
    
    private async Task ProcessRepeatOrderAsync(WebSocket twilioWebSocket, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        _shouldSendBuffToOpenAi = false;
        
        var holdOn = new List<string>(){ "UklGRlRkAABXQVZFZm10IBIAAAAHAAEAQB8AAEAfAAABAAgAAABmYWN0BAAAAABkAABMSVNUGgAAAElORk9JU0ZUDQAAAExhdmY2MS43LjEwMAAAZGF0YQBkAAD/fn5+fn5+fn5+fn5+fn5+fn59fn59fX19fX19fX1+fn7////+/v39/f3+/v7+/v7+/n59fn5+fXx8fn5+fXt6eXl4eHt9fn56d3v9+vt7dXj99fZ7c3j59PpybG5yd3Z1cm5oaW747f19/fbr8vvw5uDx9nfv1tfi+m5ue3b04P1q8Nzd1/xWZGbr0tjf8FZOWmFy9Vxi59LOdGFW2Kuy4D03aLfCQEQ+XHA9P/fKWE8hIz/l2zkhILKmtysjVa6uXzirnpiePC1XwODyu6Sj9Ckt0Kqs6uivq7s8NdC4w0Y8R9G/QTAmLDRrOCljJRIav6OmOhpXqa3ISculp/Q4RaqfrfBH2by9VkWxp6uyxsXSOS5NvK60Pyw5TmlXNzMyJywzLUYqDxrwvKZwN8awyWxSTaKfrr7WsKu/RFqtprxRZa2nutrMwVIzRK23Lyk9uro/LScvLyclV0YaFiBrvewxsZ2pRDzfq6vdsZ+jvMS9v7e5z720sKzJV8//SE7jzuA3JzT3VE07KCUoJSclLy8UG7ep2/i3n603MquiqLivoKS65rKzv8OxsK+9VMXEQjtUwtM8MjwyMk5ONSogIComHSghFTCsusK1rKCzONWpsb+qoZ+otrOtw9+4rLPU4sbCPjbhy0o8PDhANzRFOCclJSQhHSMlFjCvv8atpKa1RLurtMmonqWrr6motNixrMdVzL1WQOm840JCQ0cyLjw7KCQoJiAeHyQWHbq/TrKfqa2/u6yvzLSgqK+qpqepsrixu2RVyWRK3sHTaVJGVDsuMjUrJyUlIB8cIhoX9LdF6p+ksLS6ray4zqejtramoqirtq+v2EDn7j5UydRjZ1FMOzMzLyglJCIeHR0fHBtNttl3qqCnr76up6zNuqeqtK6jo6awtrK6bUVRVFlq62FOTUlANi8uLSgmJSIiIiEdITZp6ei8rKWlqaqop6yvs7e1sa+wr7C4vL7E0ufvY15tWUU+Pj49PDs4NzY2MC4sKiclKCstLjM+Xse3rqqnpKOhoqSmp6iqrbW6vL7FztpsXFJHOzUzLy4tLCknKCgnJSYkIyUrMTY+UdC7rqeko6OhoaKjpqmsrrC1vsfJ0vJlYVVNSUA6Nzc0MS8tKikpKCYlJSMjKS83PE/av7GppqampaWmpqirrrCxtru+wMnT2ud4XlNGPjs5NzQyLywrKyooKCclJSoxOD1N28O1q6enp6amp6enqq6vsba7vL3DzdPY53plUkQ+Ozk2NDAtLCsqKCcmJCMnLjQ6RXLLu66op6inp6mpqKmtr7K3vL28vsjP193p9GtORT8+PDk1MC0sKygnJiQiJCszOD9Z07+yqqeoqKepqqqprLC0t72/vbzBzM/W3OfqZ01FQD05NjEuKyknJSQjICIpMDU7TtvCtaunqKmpqaurqqyvtLa7vry6vMTKzt3q7HRRRkA8ODUyLispJyUkIiEjKTA3P1vPvrSrpqeoqaqsra2tsLa5vMHDwL7Ax87Z4/J8YU9FPjo2Mi4sKSclJCIiIygvOERwyLuxq6alpaeprK6vsLO3ur7FycfExcfK0+b8bltNRD04NC8tKyknJiUkIyQoLjY+X83Atq2npaSlpqmsrq6vsrS4vcfNzc3OzdV1a2VcTUM9OTg1LysoJSUnJSIfJCw2QOu6trauqKKjp6yvtbi3tra4t7e5u77D0Nvlel5jY1NBODU0NC0nISAiJigmJCc16Lispqatr6unrL/f1r64tbi6taymqrjT6vd4XExES+PL3Uo9OjgwKCEeHyEkIyIlL07MvK6mpaywrKq339u8tb3Cu6+qp6mwvcC8yVVBUOLT5GVWUE0/MiomJCEfHh8iJCkvQ9+4qKOps6ylqc1RyrO8ftm3rKysrrG0trzvTmTT9lNs1OhOQzw1LCcjHx4fHx8hKDFE7renpKyyp6KuXFi6s99Cza6rs7Grqq65v8zNzuZSX8rPTj9NTjYpJicjHhweICAjLD1wuqqmrK2jobD1zLK5UEW+rbO9r6apsbi2ucPR+mvczWZCS2BIMiwrKiMeHh8fHiApOEnZsKirr6afqcHHsbP+R8uxt8e4qamzubKwvdHW0s7U91NYaUs6NDAsJyMiIR8fHyEpMzpSua6ys6afqru2q7HuYL61wdG5rK+6ubK2wsTG1dTU7lJMUUY7OTYvLSwsKicnKSgmLDY3PVrNvb26sK2vtLWvr7vAt7S8xbq1vsTAv8HHzc3O1d7ualdVU0dASEc8Oj8+ODY4ODMwMzU0NTk9QUhSZu7czcO+vru3tra2tra3ubu7vL/ExMXJzdDT2eLwfWpfWVRQT01KSEdFQT8+PTw7Ozs7PD0/QURITVNbZvrk29TOzMrIx8bFxsbFxsjJycvOz9LX3N/n7fZ5b2xpZWJgYF9eXV1cW1tbWllaWVlaWlpbXF1eX2FiZmpsb3n89u/s6efl5OTj5OXl5efp6err7Ozs7e3t7e/v7/P2+Px+fnx4d3h3dXZ2d3l7fX5+/fz8+/n4+Pf3+fr6/P59e3l3cm9ubWxsa2pra2trbW5tb3JxcHJ0c3N0dXZ4eHl8ff/6+vr39fTx7u/x8O7t7e7u7e7v7e3w9Pn28vl+/X16fnx4dnZ3fX53eHp6fP15dP76fXj98/z28Pjw6/X98/D6+vh5+n12d3l9enp1cPxwaft0Ymv6+nt6e3R4cWhobXx1XmduYH10YfRk/cxmS8rXVNTQW070zWJPzMhTOdy7TWzITG7LWFLFVD3D3EXNw2F7ytY+S9DlZmjZRUnC4mtNTszGR2nuSsm/eNjKVL/PTWzPW0HIfE7L4WDPYtfPek/IXzu+6kdm0ls91b04U7hLScnSRNvYQHG1LVWtK+u7PnTQWFPBYEfIznBsT79UW87oQ81kalpluTBXqC9HrVAurL8jtLA+NqlXNLq7MsS4OsZQ3cdCwONMbbxmStu8P9teV85HXNxi3T3OZ0nI+Uw+uGsxvrspXq4/JKLhL7xOvUc9y7w0fMU/xHVq3m6/Lr6yNEO0101svT92qB63r1QqvKgrPKtqMb3LRWe2ZzW+t0UvuK84Lai6JMajMSqcQyamxix6qzVCx8ZH5VLPujlMrkA4uvdIPbv2N7dYPNnGVjOuVDHqsz40w0nc5t09xdQ/y81GT+fAQjjKtDpAuGJFT7rOL8K6QErYv848Pa7SNmSwQUzLXstTT1rK0D1Gtsstrk1ftEQ8urUtSrm9QFjOztXLTtfY7Mc/0NjoO8i2OU/N0UpPydxx/UfIu0Avt7s+Nta8WzTKvuoz3q89N0u0zC45qrom36vbNGbM3tFBO73DP0fPtktJZ83QXTrbvlREZs9jYVtP2rtdOdu9zkVtvllgbNRcNLvaNsNJT7nLMHCtaTlTvr40QLu7PEC9Zff02P187trKYHRjv1Dt57/gUNh31dtUVd/hwEw5tsA/P8O4LUm51DRBvMdYQvbLy0tJyvRLSs3kP2jBW0XF7kbuxkJF08tSP97I4ktK3MDtRmjQzl5Qzsd8S9HDZ0jewUxL599bTWjh83Ly9c5ZXcl4QufIRknPakvgzUxT0tVVUt3jTErMZUdh1VtO1theZcTJbObPzedf28nfVWvKzlFS8nhOQUtLPD1CPjg8Pj86OkZKQkbu3866sK+spaSmpKGlrK2xvOtXRTcxMC8sKywtKykoJyYlIyUoIyMwbVXgq5+hoJqZnqKgp7LBxt1JSWp1WeXIyt/VyftEPzowKyknJSQjJiIhLUpCRbOlq6mcm6KknqW3uLfgQFR6REXbz2fVwM1+6n5ANzcxKignJiIlJiIsSENEtKetqp2dp6Wgqr64vE1AYU87TtRsYsXC5NDOWkA/OC4rKSckJCMmIi1MQVGvpq+lm56on6Cwu7DHPUxgOzlSbUhfx89ux8xOSE06LS8rJyYnJCUlK009V7OosKScoKafobO3stI8UU82N01LQFjZ6mrT0UxJTzsvMS0nKCglJSgpST5Jt6mwp5uhpZ+fs7KvzD5fWzI3WEU569dU58DeTvNlOTU5LigpKSQkKiY9Rj66q7CpnKKjn5+wrq7HTWheNTxNQjv5/lTfyfdYe1s9ODovKispJyUqKDxHPbqssqeco6Keoq+ssM1m/kk6QUU/Ql9dWOPVW2RdRz47NC8tKygoKCkrQ0w+saywppykop6jr62y3m9uQjlEQTtGVU9V5nxZWlQ+PjkwLi0qJygpKitlSlOuqa6jm6Kkn6OysbTjTGJJNUNHPkFfWVFm71NGTT41Ny8rKysmKCwsMs1PzqaorJ6bpqKfqbuzv0k/VTkyQ0E5R2dTTuBwREhONDE1KycsKiUsLzJew/2qoKyhmaCpnqa+uLlHPE0+MD1LPUV8fll74U1AQTsuLiwnJignJy44V87Cr6CmpZqeqKWhvczAbDI9RjY2T1RGceLyZlxMPjY0LikqKicoKy0xS9e9tq2joKWhnaevrrTsVls9OT5DP05ZWPJvUVdSPDY2My0rLS4sLTQ3O0/Vwbm0rqenq6mor7q8xdxuX0tRbF1adOJpYVhMRT84MzIxMC8wMzg4O0BLW+TJv7q3sK2trq6vs7e9wszQ2vD17O1wbV5TTEY/PDg2NDMzNDU2ODo8QUpSes/DvLm1sK2trrC0t7q/yc3V4+Df6fD9Y1dPSEI+Ojc3NjU2ODg4Oz0/RlJn58vAu7i0sa+vsbO2uby+wcfN09ni7HlhVExHQT48Ojg3Nzc5ODc5Oz1BSFJr3c7Iv7u4t7e4ubq7vb/Aw8XIy87U33dkWU9JRUE+PTw7OTg4ODk6PD5EU27n1cjAvbq5ubm6u7u+v8DBxcbL0dbg7V9PTkpAPj87OTc1Njg2NTk9Rlhx3s3Cvbu5ubm6uby8vb7Cvr7FxMfQ3+N5VUpHRDw6NjUzMzIxMDA4RExU38W8t7azrq6ztLW3vL2+vb/JwMHO3tpwTkpEPTo3MC8vLSsrKCo1ODpL0MO4sa6rqautrbG6vr3Fw8jLxMjV699hT04/ODUxLCsqKCchJzAzNFu9ua+opaOhpaepsby/yt/o8N3N6nnd3k9QUj43My4rKCMmHx4qNC5Et7Oto56fnqGkqLLExehLVVxV6e9v4N1YV1A6MzAqJiUhIx4fMDgvz6y0qp2fo56hqa65zflFR0g+S2xJUe5cSllEODYvKSckISMdKTwwRKuusJ2cpZ6dqa6yw3tKTUw6TPJDS9xURFtLNjk1KyoqJCUkJj84Ra6rsp6cpJ+cqa6uwG9YWkc8VWk/TfFDQ1VCNzs3LSwrJiQnIjI8OMOqtKecoqOco66rtu/fcEVIUk5MSFNKQkdDPDg4Mi0tKycoJi1AOPCus66eoqaeoa6qrtXQy0lM7EtIXkZCSUI9Pzs3NzEvLSsqKSk5O0K5sbajn6ifnqqqqb7OvmpK41hBV0k8REI6PDs0NTQvLy4sKyouOj1eu7mypqWnoKKqqay9wMB+aepPSU0/PT87Ojs2NDUyMDAuLCsrLjc+ZMS8sqmnpqKjqKmtuLvC2uR4TkpFPDw8ODg5NDM0MTAwLiwrLTE6R+jEuq+qp6SipKeprrW6w9LgX0xHPjk5ODY3NjMzNDMzMi8tLC41Pk/Xv7atqKWioaOmqa+1u8jba01DPjk2NTQ0NDMyMjIyMzIvLS0wOkhtybuyq6ajoaGjpquxt77O9lJDPTg0MjMyMjQzMzU1NDQxLy0uNUFT3L+1raejoaCipaiutLrE2WVLQDw3MzMzMzQ0NDU2NjU0MS4tMDtMbsq5sKuloqCho6ersLi+y+5WRj46NTIzMjIzNDQ1NjU0Mi8tLTI+UujCta6oo6GgoaWprbS8xNlfSz86NzMxMjIyNDU1Njc2NDIvLi83Rl/WvrStqKOioaKmqq62vMPXY0s+OTYyMDIyMjU2Njg5ODYzMC4vN0de2L60raejoaCipqmttLrA0W5NQDs3MzEyMjM0NTU3ODc1Mi8uLjVDVunEuLCppaOioqWprLG3vMjhWEU9ODMxMTExMzQ1Nzk4NzQwLi0xPEld1L+2raeko6Kkp6qutLnA0WtLPzo1MTAxMTI0NTY4Ojk3NDAuLzdBTnzLvbOrp6Sjo6Woq6+1usLUZkk+OTMwMDAxMzU2ODo7Ojg0Ly0vOEBM78m8sKmmo6Kjpaerr7W7xtxZRTw1MS8vLzAxMzU3Ojs6OTYxLzI7Q1Hiybyvqqeko6Olp6qutLvF3FdEOzQwLy4vMDI0Nzo8PTw6NzIvMTg+S3LPvbGrqKWjo6Omqa2yuL/UXEU7NDAuLS4vMTM2OTw9PTw6NDAyOD1IXtnAtK2qpqSjo6Woq66zu8tvSj01Ly0tLS8wMzY5PD09PDo0Ly80OT9O68O1rqqmo6KipKeprbC5yO9OPTQvLS0tLjAyNTk8Pj49PTowLzQ4OkNqyLiwrKejoqOkpaissLfD4VE9NC8tLC0uMTY4Oz5AQD06OTErLjMyOE3bwbatp6SjoaKkpamtsLfJ+ks7My4tLi8xNTc7Pj5AQzs3Ny0rMC8vP1rqwLSsqKaioaSkpautr7zOd0E2MC0tLS80NTlAP0BLQjo8NSwvNC41TVLfurKsqKShpKOiqKuqtsHJTTs5Li0vLTA4NTxHPUNNPDc6LyovLys4SUTduritp6eko6Slqaqrt7vFSkI9Li8zLjI8ODxNRERUQTg7MikvMio3TkHZt7ispqaio6WkqquruL7ETT8+Ly40LjE7NztKREVQRjs6NiwtNiwyUkFhtrqvpaeloaanpqutr7vG5EQ8NS4wLy42NzpFSElPTUY7OTQsMDUtPlNEx7W6qqSppKGoqKiusrPE3mA8NzItLy8vNTk8RE1NTlJHOzs0LjUzNElObL24saiopqOmqKesr667y8lINz0uKjIsLDgzN01FSWxKRkY5NzMzNjZHVme9t7Sop6iipammqrCus8vO3zM3NycrMScvOTA/WkNo7UhaSDk9Nzc7P1Jty7i1rKaopaKoqKits7K74OFPLTMvIywuJjA4MUJcSFzmSUtLODk6NTtIU+XAt7GqpqikpKmpq7Cyt8HWZUQvLy4jKywmLzgxR1dKafdKTUQ4Ojc2QEBd1Me2r62mpqekp6urrra5vdlbSTArLyQmLigrPTk+/XhX43VHRT43Njo9P1zQzbiurqmlqaalq62rsri2xWtcNykvJx8tKCY4OzV751bs4ExBPzkwNjo4SubavbOuq6enqKapra2ut7i50VNPLyYyIiAzJydMOzrK8Vrk8Uk8QjkwP0E+2snGsq6tqaipqamqr62utbS2x1dTLSUvHh8wJidZOj/F2V7Y9kk7QjkwP0s/0r/Dsq2uqqipqqqqrq6ss7GxwtxWMCUtIB0rKyRAUjzdv2pZ2U41Pz0zO1hN3L27tq+wrK6tq66ura2urq+rycPKLiYtHxwkJiYvP0ho49TqSlxFNT09OExa3r29tq61s6y2sq2vsK2rq6+uqd/KxyghLx0bKCcoOkhR0+PS3UlQTzdCR0Rg38q4ureuuLyvuLyvrLGtqKiwsKfcXL0qHSsfFyUrKDFdXNZp6to/PFM+N1hVYdi/vLW4sbG+uLi+uK6vrainq6+st0zCOh0kJxkbLCsvOdvYdErpYjc7Vk07aMzVzra3tbW3trzFvrW3s6mpqKWsra/DV9MrHSUiHB8tND85dcNcPEhrODlDcGFbzbq5vbiusr++u7/GvrKurKynpK2wtMNtVyUbJSIcHjA9Pz5F0Xs5MUtMPEBsxsbBv7Gwure2usDFzsG5t7Ktq6mpsLnE1mdDKB0fJCIiKTZITD5EVFE/PEBGTmLq18K8uri2tba5vL29v767t7Wxr66wtr3Dx+BALSknJiUlJicrLzI5PUJJW2p+3NDLxLy5uLa1s7GytLa3uLq8vLy6uLe5vMHHzXU/MCsoJCQkIyMmKzA5Q05Y+NPJwr++vLm2s7KysbCxsrKytbe6u7u5ur7H0u9hSjYrJyQhICIjIyUsND1LX/fcyL66ubi2tbOxsLGysrK0tbW2ury8vLu8wM3mZU5ANSsmJCMiIyUmJyw2Qk5k6dnKvrq5t7a1s7GvsLKysrO0tLW4u72+vr7Dz+pbSz82LSgkIyMjJScpLDI+UXTcz8rCu7e2tbOysK+vr7CwsLKztbe5vL7AxMnS7lZHPTcwLCglJCUmKCosLzU/UHvaz8nDvbq4trSysbCvr7Gys7S1t7q8vsHFytLjZlBHPjkzLy0rKSkpKisuMDU6P0pe49DKxMC+u7m3tbSzs7O0tba4ubu8vsHHzdHb7WxbT0hCPzw6NzY0MzIxMjI0Nzo9QklQXX7d0MrGwr++vby7uru7vL2+v8DDx8vP1tzj725eWldTT01LSUhIR0dGR0hISktNTlBVW2Br/e7n39zZ2NXS0tLR0tXV1NTV1tfZ297i5+rr6u7/eXhybXJ4bmtvc21pbW5tb3h5dG9rbHR7eXN5fvfu7Ozq5+bg3+Hk5+Xj5Ofr6+vu9vHu93z9+3x8eW1ubm9ta2ptbGVpa2dyZW5oYvtqaWt6dH39eu/9/fl5cXr+e29tc3H8+vx6ePb88PTy8up69+bu+PD+eHh+73x+8/L99uvt9ur79evl5+r76+F8cezxcGz9c3jo7G1hcPl2dWdi+fpqbnVh+eVubO/r8eN2beV4ad7ecWXv6NngaWfme3l9dO1bbfTsVvNlau9Yde5+7epx/H7kdF1eVV9abXjt6fNm5tdr8mpOedxl6+xpXnPIX3Lk69huV/ng7+d4bfXl7m7pZGDPxM1p+83Nz9XwWuLv9vlf49nZdWvN7eB6/nRSW+jpY2JI0HpJW/BlS0hv2HFrRVDS7VpSYGNZ2txJTlJod2/mXlFc5F5RVfTuaNLS+evdY1lZXGBaXVrp+mVs3ePgclhyzOxXUO/+4NRb79jz98TRbNreaeXMz2tb4szL2NTd7tjT1ORZYtLSbPzn5ePk6uPubm3d3V5dXOrj7O7iXVPb9//6Slfl3ddUS2zcb05VYPflaGRr7fjl/mdnXWjzbllde+rr83h06N7yXvHa7mvu9l3v1+n8d+na3OL6cGf719ZuVGLc0edfXWjs5fZjUmPf7GJbX+/fdWBocenj+3Xz3+p+cG9oa/9uXXPm6nVkde/6YWr1aVVVeN32Xltq7XxeWGz49unj721v69va3+rp3drtaWBmfP1kWV9z7et3aG/u+VhMT2f9YlRXcuPg5OHc2tjSzMjJzMvFwsXJycbIz9fb+E9EQ0pORjw6Ozs3Mi8uLS0wNj1Ic8e5sa6rqKmrra2trrO3ur/HyszS5mVTS0pPTDsuLDM8Lx8aGh8mKjFQu6ulpKerrq+0wNfPwLy7uLGurq+0usPM2OdrV1BUZe3yTzktKy0tIhoYHi0/SXC8raqrrrbCysXDztvMvLOvrq+yt7zC12VaYWpfVll+3fNINi4sLSkfGhsiMD9M2rqurKyut8PJwcDJzsS4sa6ur7W6vcXeVEtTZWdaWWnm5V1AMy4uLSgfHB4pOkthzry0r66utb7Cvr7Cxb+5s6+usLW6vcLQ/F9kbmZWTU5ZXlNDOTMxMS8qJSMnLDM6Q1R9z72zsLK1tra3uLm5urm4tre5vL2/xcrP1dv0XU5KSktKRD05NzY1LywpJygpLC81O0ZvyLqzr66tra6urq+ws7W4uru8vsLJz9XY4mpRSUVDQT46NTEwLy0rKSgoKSwyOT9L5sC2r62rq6yrq6yur7K2ubu8vsTJzdPa3d9yVUtKSEM+OjUxLy4sKScmJicqLzY7RXnDt7CtrKyrqqmrrK6vs7a4ur7GyczT293hbVZPTUtFQDs2MjAuKygmJSUmKS40OEJ5wrawra2sqqioqqytrrCztbvCyMnM2uPwbVxXVk5JRUA8ODQwLisoJiUkJSkuMzdAcsO4sq+traupqausrK2vsbO5wMPCxtDZ3fB0cmlWTEpFPzo2MS0rKCUjIyQnKy80PFjMvLaxr66sqamrrKysra+yuL7BwMPL2t7c3ePwbFpUUEo/OTQvLCkmIyIiJSgrMDlFb8W4tLKvrauqqqusra2tr7S4vL6/wsrW2dTW3ux1XFRORTs1MCwpJiMhISMlKCwzPErfwLm1sa+trKurrK2trq+yt7u+vr/Dys/NzdPa3epnWU5DPDcxLCglIiIiIyQnKi44SG7Tw7q0r62trayrq6ytr7K0tri8v8PBvMDN2NfU81dBNzMuKiQgHh0eHyYxSGhRfbqzx+DIurO5vreopKusra23xeVvzNrbwq+qrbCvrrtpPjwxKCMjJSEeHiQmJzNk10dJxcM9QcjFzMq1rauvrqWotbixwdvQysG9ua2orrGtr8fpe0o6Li0rKSQgIB8fICc2UkA+yb87Q8hrStO7vbe3rquus66yysu/ydbBtq2trKior7i2xFxDPjUqKismICEhHyEqPkk5TrxDNGdYOVHOz8C+sa6ys6yzx8C/0c/FubOvqKaqqaqwuL/fX0g1My0rJiEhIB8hKUJQNOTJPDpgOjdSW+PKu7Gwtautvry99NvEyr+zqqeqpKKrrqy6391POzUyLiciISIhICdLQTjf0zlASjc6P0x04cG0u7aus7+7v/Pox8nJt6uqrKWiqqunsMDC5EQ+OTEtJB4pIhwpNzE59mM/QlE0MkA9N1LT5c25sr2+scLtvsXnxra1sa2nqKqmp6yussTV40Y+QScjLCMeKS4rNUxEPlBJOTc+OjI+U0hfxMPGu7fBxbrBzr64v7itr6ypqqinq6ywur7acvQyLDMnISooJzA1NDpDQjw9Qjw4Pzw+U1pf0cnKv7++u727vLe2ubKtsK6pqqyprLKwu8S+cDc/NCgpLicoMi4tNTk1ODxBOz5MPj9ZWEl1y+DMvb68ubW3urO1u7OwtrCssK6vrrS5sr5UZEIvMC8oLSsoLiwqMS8vPDk9R0dIT0hPZ0tj0tjKurm2sa+ys7K0uLezuLiwtrWutbSwvc3bTj02Mi0tKykmKSskKTUuMlNJR+7eVGXUaVjMx8y6sbaxrK+xrrK3ubjAxLu8u7SzsbK5usL5VEQ1Li4oJCUkIicjJjYxM+9vSc3XUubZWu/GxMS1r7Wvqq2vrbG6vL/Hzr66wrSvuLKxvMXIVzs2LiYkIx8eISEiKDU4PNDca8fHas/K6su8vrivrq6rqquurrO8vcHExMC4urmxtru5xe1MPi8pKCIeIB8eIiYoLzxMWf7D0O3AyOzEvMu4sLWvq62sqq2usba1vL+4vb66uMG/vNx4Yz0yLiklICEgHiIlJiszN0Zs9dzLysbMxrzFvrS3uKyvsKusr66utrm6usbEu8nMvtLw0l5IQzkwLiomJSQkIiYpKC44N0V2a9jFx8C/wLy7vLm3tLKxrq6urq+vtbW6xMPH6+DRXHDoVUxXQDs7ODQtMC4pKi8pKjUtLz8+PHNSfNDF0MO6v7u+tLu7tbG8tbG9r7i8vbLFx8TTz0zWVkBUST8+RT47OE43Nz1FMTc/PEA4TE4+XWXwQMHgY9LD0O66v9XOuLjwu7vHx8qz3mi0xF3Lw71Fy9DWQc1WPr84Wms9UUVUOsUzZ1M/2DbFPlo8tz0/x01vTcdV1V7LVMdK07w9t0i4Pb7wwELMvFhNwdQ3qi7Dxm/dQsrN3CimSy241k1FvDu/V3bHOs3PSfTY9F/IPL/lYfVtvDayTvNS/bsysC6sM87YfF111VVY9LkrtuE9Ts3IN8nOP7w2ry/EeWDxNqwqsjn5tC28Q7oww8I1tzy7TUC1QN7lZcI+ecDOOLhEvEfq783lNbJC0UnEX3hbv0xEw1rQOLQ+c9ljwzm/OK0vx+lH58rROK8utFpbY9jMNbZtPLdG4MEyrDXJXchIR7k9zE3FQdlSwkpgZ8bgNrRI2UC4L6stYaceqUpW69JcUrw4vlZawVFBt0XeZtpZ00DI1TKuOFy5Lq4+TdXGVzWuMq8xwsg6xXrqZ9BPx05Ruj7H3kXI4H1Mxc47vE5ZzEvZ30K9QctG7rs1vT7BU1DT614+vdksrUfs1Tu0PNlIwVBMxT+9R9fYR7w2vu5TctVfvz7OzzinHqQ9a8Qxpyq2RGu7LKw9XbU4xU/ETUO+Tcs8ycM83LhIO6oy3L8+x03JRcM9v1xg3WHEd0S9ZkTDTOxPzVL4yDm8TUSvLbplU07EWTixLbjeQuDY5kC5SWHXRLs5/t7MS13DQM9H0L8yxsM7xFLdvjhlvFpK2781tz7kuC26b01tyDzVtyWjNVu8TfxfvSyuSUO2SlK8Rb5Gvj7UsierP+jhXMRGwjrI20rl7tRmVN3HWzm2U1XzUtJwQL9Gw0Jcsy/TZtlOStntxDTIt0FGvVDdYlLW2VRNvEbQ3lnMXVHCTFnPXtxP1U3aa1nj4Mw7vljhzEW+WdJYWbkuvNkzrjDOxk15Tro/4dk+tDTrwkhqYNVK2Vlj0E5r2Wxwa1rIR97r2mrm1VTU2UrT2E3FXFK+StZx7NZCxU3YSXHJSc5Oz3te31HDROnVTtFGzfFX1W3NU3XLRNduV9hbWOb1T17s31tg4mTjT3LMTd/dX9Ffb9ncW+PSWOB3WdBba9Jn3uhr1vRi3Nxf7vBi7X32XNppW9NRZ+RPcv1T8FxhZlZsWF1bcG1Y7Gbv61zb5fnobtfV/tDPz8rYzcjY08nW0sjazczv1t5WWk1APjo3NjUzMzY2ODw+RUtPXnjt287Iwr66t7WzsLGwr7GwsLW2t7m6vcHDzOtkUEQ9NzMvKicmJCQjJSkrLjM9SVZ03tLP09DLyMbDvru5ube0tLW1tri6u7y8vLy6ubm4t7e4u77E0m5OQjozLywqJiQkJCUnKy40O0Zh3s7HwsDBxMTBwL++vr28vLy7u72+v7+/v7+9u7q4trSzs7S1uLzF12FHOzQvLCknJSMjJCYoLDE4P0/60MbBwL/AwsTDwL/Av76+vr6/wMHGysnGw7+9ure2tLKxsLG0uLvBz3xNPjYvLCooJiQjIyQmKSwxOD5McdPIwr+/wMLFx8TBwsK/v8DAwMPFyczMzMjAvbu4tbSzsrGxs7e7v8fUcEs+Ny8sKSgmJSMjJCYpLDE6QU5z1cnDwcDAxMfIyMbExMK/wMLDxcfKzs/Ny8a+u7m2tbOxsLCwsra6v8fRfE0+NzAsKSgnJiQkJScqLDE6Q0951snDwsPDxMjKysnGxcPAvr6+wMPFycvKx8O+u7m3tbSysLCwsrW4vMHI1WpLPjcwLSspJyYlJSYoKi0yOT9MZ97OyMbFxcfIysvKyMjGxMLBwcTFx8vNy8nEv7y6uLa0srGwsLK0t7q+xc/sV0Y8NjAtKykoJyYnKCkrLjM5P0pd69rTzszMzM3NzMvMy8nHxcTExMXHyMbDv7y6uLa0s7GxsbK0tri7v8XN3mpPRDw3My8tKyopKCgpKy0vMjY8Q05g/ezl3dnY2NjX1NHPzszKxsTDwb++vry6uLe2tbSzs7S1tri7vb/Ey9blblZKRD47ODY1MjAvLy4tLi8wMjU3Oj1FT19seO7h3NjV09HPy8fFxcXDv7++vb29vLu6uru7uru7vL7AwsXKz9bh9W1cUExKRkE/PT08Ozo6Ozo6Ozw8PT4/QURHSk1SWF9w+Oba1NHPzMrHxsPCw8LBwcLBwsXGxsjJyMvP0M7P1Nbd5uXp6HVoamJcWlpXUlBQUE5OTU5QTU9TT1FRVFVcWVxgX2ppcnh2/Obt9ubj5eTh4ODd3uXh6OPd5uLs5fLq4Or89urm+/Pw++x37ufv9W766fxu8Xlk+fH+dHZu9Of5e3n87/Dv/PPw9Pns6+399fHv5fR3ef3ueu9s93Fv+Hlwam5tbP5wX/v6XGb9/WpsdWv0dnJpdXtxaG76bG1uffJtc3v5bvD6cuzz8nD07vjz8vj3cPni/HX2++1+93Pv/27w9X1ye/T2ffhqa/b2cG56bW/q8H5ocfrm/Hhwae/g7Xt+bOPp+njx+HP37PB+ffrp/PF1//T3dHH2a/Dwb2x3+eZva/n3fXjremN28/fsW3fnXej2av15ePPramnu6v99Z/T2//n0cG53+vB9dWj+7nJ9d2/26mtl7PD6efV0dfLtcO7xePN49udw6/Z29+p+cvzod/12+vV1b/718m5qa/vpfGRr/PhydndyeXluYOb4b/V0enNx9+pxcnd67fP3+nd9bejrdm7s8/Py8v377l3i6/dl9t96aev1cPn983Xn7m795Gpp+OrxZvTzeO7ifGnecmnk7XVv7m/9eOr8bm7r4nl27f5q7nVxeHf0efBmc2366mLpYGxu329eZV3g8vhgbnZ87X1pcH3+YmH64vt0eV1v8OLtXXj3Z/Dd421lZODp/eT4Xnrl+fd1cXV2+/dl++Vb3HT42WZt5e1Xau3e6nVd/9jc6Xh5ed/gemtzbOLac21vbnFy5+T9eXXr5npc5ur5X2Pu7Gd2a+r5/eRdYHh7cltd4u9Z6tfjc25udOD3Xmlwc3h+3ObscHP2eF/14fhy9/1gX2dw6tz0Zl9v3tnf4f7v8359Y/39aP5iaOfsdfDpfHv3eWRabt/ka2JgYvjve2xmZ259+fV3bV1t6N7i6vB6evt2c/jw7Hpz6ent8+vmb2h0dWNgbvHs+2pr9O7t7/t+e3hvaWttcfrn6Ovr7f3++Xd1ffLt7ndiYnLu5Obt7Ozz+PPt6uTk7Pv36+vq7PxtYFpXVVJSVVdXVVVUU1VdYmRjX15gYWJpeO7e1M7KycjHxcPBv76+vr/EzNfi6uTb1NHU4mJJOzEsKikpKiwvNDtHXt3MxcXJ0N3r9vbw7vh1bXffyr20rq2trq+xsrKzt73K6llNTE5VXGFgW1JKQjw1LikkIiInLj/ivbSxtLvF0eHy/XBcTkdDRExi28i/vr/Cx8rIxL+8ube2tbW2uLu+w8nO1dvh6/b79fRwWUk8MywoIyEhJi0+4ryzsLS8yuVgVlNPSkQ/PkFO7sq+vL7I1+333cq+ubW1tba1tbS2ub3F0N7p6eTc19fb615IPDMtKSYjIiMoL0PUubGwtsLhVklFR0pKRkNES2vNvrm5vczwXFhyz764tLO2t7e2tbS2ur/I0drY0c7NzdHfbE8+NS4qJyUkJCYrNk7MubO3wN5OPjs9P0VKSkdIVeHGura5xONTSEtozb23tLW3uLa0s7O2u7/CwcC/v8LHzNDbeVJANS4rKioqKikoLDZRybezucxYPjg4Oz9CREZKVt/Eu7m7wdVhT05Xd9XHv725tK+trbC3v8fIv7q3ub3Dys3MzNpcQDQtKystLzAvLCotPO6+t7jDdkhAQD47Ojg5QGTIu7i6v8vY293xXU5KTW7KubCurrCzuLu8vL6/vru5tra5vsnT6lhDOC8rKistLzIyLy4xQeDDwMjfV0tMTEE4MzQ6SN6+uLq/xsnJyMziUkRFVd3Fu7i3trGurrK6wsfDu7a0uLy+v8HG0WxIPTgzLiwsLC0wNDIuMD3txcTUZ1FZ8PZJNC0vO1nRx8jLxbu2uMT1TUZNZH5lXPTFtKyqrbK3uLe4vMDEv7m1tbq/yM/dXUU7NjIuLCwsLjE1NC8xP+jN6VlZe9/iWT0yMTpGT1dy1MW8uLrE09ncflROU1ph68q9trOysrKwsLW6u7m3tre7w8fDws15T0Q+OzYuKywvMi8uLi82TNngU1bSxNpMPjw7PDw7PU/Ovr6/vLe4wtTh7WxTR0RP4srEv7iysLCxsbOzs7e6urm8w8fGyM/jYVBJPzcwLy8uLS0tLi80P13nZmnJvM5KRE9GNjE4QE5k4s29tLbAxL2/6E5PVEpGUfvTxLy6uLCtrrOxrq+3u7m4vs3Ox8vrVU1KQzozMTIwLSwuMC8wN0Ru3O/vw7zsRVV0Oy43RTw4U83M0L+2ub/DwcjpVFVcUE1t1M/Gu7WyrqysrKytsLW5vsjO1etiVU5FQD87NDIyLy0uLi0tNDxDT+DS5s3C105bdj0zPUg4OmjfWuC8v8vAucXaz9NoVGxyX3PYzMW9uLKwrqysra6ws7e+x8zgVktKQDo5OTUyMjAvLi8vMDU9RURf9ujU1u7n30lFUkU5QVdDRPbcaNfBx87FwM3Rys7e38/R2My/vr62srOvra+vrrK0ub3DzvBhUEM9OjYyLy4tLCwtLi82RT5L3t/kzMfe3vBmT0tIRkRGSktTW2zt3NnLysvGyMnJy83M08/LyMbBurq3sbCxrq+ysLW4vMDJ13RaSj87NzIvLSwsLC0uMTlCQGfq29HKxtPP1+peXk5IRERAP0BDRkdRW2vu2s/Ny8TExsLAv7+9vbu5ube3t7a2t7a3ubq8v8PJ0OJhU0g+OTcxLy4uLi4zNj1AWGr4zsrLzsjX5W1nS0VEPjk7Ojc6Ojw+QkdOUG7q3cvHv726t7a1tLS0tba3urm7vr29wMHCxMjKytHZ4PxdT0lEPTw7Ozg9QT5IU1Rfe/TwbvNfVlBLREI9PDk3NjU1NDU3ODpCRU/918i/ubW0sK+ysbO1uLm8v8DEx8rNzdPV1NfY2Nnc5OXvbWRuUlNeVFtj8u7g0NDWzdjf9WdRSkM+OTc0MTAvLi0uLS4wNDc+SWrhx7u6s7CwsK+ztrm5wMLFydHSz9/b19rb0NLTz83U0NXa3N/f5eDa29PP0szO09jbemFSS0I8OjYyMC8tLi0sLS4vMTU9Pk3Z6MO6uLaxsrS1tby/v8jP0M7a2c/S18vLzMjFyMnIyc/R0Nvf3ODZ3dDNzsfGyMTGys3W5GlVSUA7NzMvLi0rKysrKywuMjY+WE/PwsK6tre4uLrAxcTW3Nre89/W2NLJx8fAv8K/v8TFyM3P2dPa3NfPzMrFwMHAvcTGyNbqblBIPzs3Mi8uLCsrKSorKy0xOUZG18bPt7W7t7a/v8fM5vDtZV/o5u3Ly8jAv76+v7/DyMjO0tXY19nQzMvEvr67urm6vb7E0N9vSkE8NTEvLCsqKikoKyssNTs5YNjnwrq9vLi9xsnK/nB1X1Je7GnszMzNwL7DwLvByL3F0MbH0szFyMzBu8K8s7m5s7e+vMHa72BHPDo1Ly0tKikqKCosKjI/OFPO4si2vr+5w9PS61JTTEtLUVt+58/Fw8C8vcK9wszKyd3b0NDWy8HDvre1trCvsbKzuL3F0H5NQz01MTAtLCsrKiosLSo5PjdRy3DetsbQvsRZ4epISlxMSmp039/HwcHDvL7Iw8XP6tDWfuXL09K+vLy1r7Gvra6ytbe9zepsRzw5NS8tLSwqKysqLCwvPD485stnxbbK1LzOUP18QUVeSEv/4+TJw8C6ur28vMfGyNjfzt72zMjKwLi4s7Cvr66xtbe+zNl4Rj88NTEwLi0tKy0sKSswLjFLSD/MvN/GssHnxM5PTlpHRExOYO3kzb3Av7i4vr2+ycvM2uDX4djLxr+5trOurq+ur7a6vs11Wkg6NzUvLi4tLS0sLS0sLTI0PFBTWcC8zcO3xvDb7k1GRkVPTlPfx83EuLe7u7m+x9DS1upm59XWzr66t7Kurq2ur7G4v8fWVkdAODIyMC4uLi0tLi0tLi4vNz1FWe7Ov7zGv73NYWhcRkBFTFFccMzCwr+3t7y+wMbO3Prv6v7w08e+u7ewra2urq+zusLN4FVBOzk0MC4uLi8uLS4uLi0sMDs+RFTdyL/Dwr3B3G54UUZFSkpa997NwL27uLm7vL/HzNTb2tjb0cW+u7ezsK6usLGzusHL23FQQj08ODUyMTAwLi0tLSwsKy44QEVU3ce9vL/Bwc3qX09JTE9SaNrLwry6uLe3ur3Ezdbe6/f56tXHwLy3s6+ur7GytrvEz+hsV0lAPTs5NzQxMC8uLSwrKystMzxFUv3Vx7+/xczX63ldT0xSZePRzce/u7m5vL/CxMrS2+Xe1MzIwr68uLWzs7S2uLzBytLicltPS0dCPjs5NzQxLi0sLCssLTM7RVVt5dPJxsnS6WphXVtaW2jizsW/vr28u7y+xM3T1dTSz83Jw767urm5ubq7vb/Ey9Ti+G9jWlBJRUA+OzczMC4tLSwsLC4zOkBKU2Lv2M/P1d7q7OXe3NrX0MnCvr29vb6+v8HGys3NysXAvr27urq6vL7Aw8XIzNHa4+1+alxSTEdDPzs4NDIxMC8vLi4wNTtBR0xQW3Xk3N7qfHP759vV0c/NysXAvr29vr+/v7+/v7++vry7u7y+wMTGyMvP1t7o+W5gWVFMSERAPjs5NzU1NTU1NDU2OTw/REdKTldj/ejh4eLe18/Kx8XDw8LAvr28vb6/wsTFxcTCwcHCxMfJy83P2OT6bWReW1VPS0dFREJBQD49Ozs8PT09PDw8PkJHS05UXGj96N7b2dnX1M/MycfFxMTEw8LAwMHDxcfHyMnJycrKy8zO0tfc4uv2eGpfWFBNTEtLS0hFQkBAQkRFRkZGRUZISUxPVlxmc31+9OHWzsvKy83NzcvJxsXGys3P0M/NzM3R2eDn5+Xp83dqY2FkZ2VfWlVSUVFQT09QT09PUFBSV11gX1tXV1xs9Ojm7Pt88OLa1tTW2tza19bU0tHT1dfa3d/k6ezr6u3x9/96/vT1fXFta2hmZWNhX11dXl9eXl5dW1pZWVxhZWRjY2Nkanb68/X9eXv37+zp5uXl4d3b3ODn6ufh397h6fX9+O7p6O32/np5fPz6/P3+e3NsaGdoaGdmZWRlZWZpamprcX39/n16eH76+vz9fXv77urq7PD5/fnz7+/x9vj5+/z79/Lu7Ozu8Pb7/Pr5+fr8/npwa2lqa2xsbGtpZ2dqa2tsbnBvbm1ucHZ8/Pr9e3h5e3x7fX5+//z59vPy9Pf39PDs6enu9vv79e7s7fP9eHZ5fH79/Pv9fXt6e3769vX3+fn5+Pn6+vr49fTz9fj5+fn49vPw7u/y+Pv69vHu7e7x9ff49/b08vLz9vr9/v38+vn5+fr8fnp3d3l7fHp2cW9ub3F0dnRzcXBwcXR4eXl4eHl6e3p4dnV0dXZ2dXNzcXBvb3Bzdnl6eXp8/vr18/T2+fr5+fj4+fr8/317fHt8fHx8fHx+/vv6+vz8/fz8/P5+fXx9fn59e3l5eXp8fX1+fX19fv79/f3+fn18fHx9fX7+/35+fX19fn59fHt5eHd3d3d4eXp7fH1+/v39/f39/v7+/v38+/r5+fn5+fn6+vv7/Pz+/v9+fv/+/v7/fn5+fn17enl4d3Z1c3FwcHBwcHBvb25ubm5ub29vbm5vcHJzdXZ2dnZ3eHl6ent8fX19//7+/f3+/v7+/fz8+/v6+fj39/b19fX09PT09PPy8vP09fX19fT09fX29vb29fX19vf4+fr8/f3+//9+fn19fXx9fX1+fn7/fv/+/f39/f3+/v9+fn18e3t6e3t6e3t7e3x9fX5+fn7/fn5+fX19fHx8e3t7e3t6enx9fv///v38+/r5+Pj39/f3+Pj4+fn6+/v8/f39/fz8+/r5+fn5+Pj4+Pj5+fr7/P3+/359fHx7enl5eXl5ent7fHx7e3t7fHx7e3t6enl5eXl5eXl5eHl4eHh5eHh5eHh3d3Z2dnZ2dnd2d3d4eHl6enx9//79/P38/f39/f79/f3+/v7///9+/37+/v7+/v39/Pz8/Pv7+vr6+/v7/Pz9/f39/f39/f38/Pz7+/v7/Pz8/f39/n5+fXx8fHt6enp6enp6eXl5eXl4eHh4d3h3d3d3dnZ2dnZ2d3d4eHl6e3t7fH19fX5+fX5+fn5+fn5+fv/+/v38/Pz7+/v7+/r7+vr6+vr5+fj4+Pf39/f3+fn6+vv7+/v7+vr6+vn49/b19PTz8/Pz8/P09PX19vb19PTz8/T09vb39/j5+/z9/n5+fXx8fHt8fH19fXx8e3p5eXd3d3Z2dXV1dXZ3d3h4eHh3d3d3dnZ1dXV1dXZ2d3d3eHl7fHx9fX18e3t6enl5eXp6e31+fn5+fX18e3t6eXl5eXp6e3x8ff/+/f38/Pz8/P39/f79/f38+/v6+fn5+vr8/P7+fn59fXx9fX5+fv9+fn59fXx7e3p6enp5enp6e3t7e3t7enp6enp5eXh4eXp6e3t8fHx8fHt7enp6enx9fv79/Pv6+vr6+vv7+vr6+vn4+fj5+fr7/Pz9/v/+fv9+fn5+fX19fX19fX19//79/Pv6+/r6+/z8/P39/f38+/v5+Pf39vb29vf29/f39/f39/f29vf4+Pj6+vr7+/v8/Pz8+/z7+/v6+vr5+fn4+fn5+/z9/359fHt7enp6eXh3d3d3eHh5eXl6enp7enp6ent7fHx8fXx9fH18fHx7e3t8e3t7e3p6eXl4d3d3d3d3d3d2dnZ2dnd4eHh5eXp6e3t7e3t7fHx8fHt7enp5eXl5enp7fHx9fX5+//7+/v7+/f79/v9+fn59fn7//v39/fv7+fj39fPy8fHx8fLy8/T09vb4+Pj4+Pf4+Pj5+fr5+fj49/f39vb39/f4+Pr8/f3/fn18fHx8fX7//fz7+vn5+fr7+/3/fXx7enp5eXl4d3d3d3h4eXl5eXh4d3d3d3d3dnV0c3NxcXJycnR0dHV2d3h6e3t9fHx7e3l4eHh4eXp7e3t7enp6enp7e3t7e3t7fH1+/vz6+Pb19PPy8vPy8/T19/j5+vv8/Pz9/f7+fn5+fX7+/fz5+fj3+Pn7/f7/fn5+//9+fXt6eXh4eXp8fX7////+/v7+/v38+vr5+fr5+/v6+vn39fPy8fLz9ff6/P5+fHt7e33++/fz8PDw8/f7fnx5eXt+/fr39vX09PX29/n8/3x5eHh6ff369fLw7+/x8/b6/np1b21ramtsb3N5ff7+fnx5dnR0c3Nzc3NzdHR1d3h6fH19fX59fXx8fHx9fn5+fn18e3t5eXh5ent9fv39+/r6+vv7+/z8/Pz9/v9+fXx8fH3//fz8+/v7+fj39fP09fb5/nx5dnRyb25tbW1ub3J2eXx+//7/fn17enh3dnZ2dnh5e33//fz7+/v7/Pz9/fz7+vr5+Pf39/j5+vv9/318fHt8fHx+/v37+vn5+fr6+/r5+Pf19PPx8O7t7ezr6+vr7Ozt7u/x8/b6/H56d3VzcnFxcHBvbm1sa2tqa2ttbm9ydnl8/vz7+vn5+Pj49/b08e/u7ezs6+zs7e7w8vX5/H57eHVzcG5tbGppaGdmZWVlZWZmZ2hoaWlpaWpqa2xucnj+9e/r6OXi4N/e3dzc29vb2tra29vb3N3e3+Lm6/N9b2hgXFlWVFJRUE9PT09PT1BRU1ZZXWRu+urf2tXRzszLycfGxcXExcXGyMnLzdDV2+PwdWVbU01IRD89Ozk3NjY2ODo9QEZNVWBz8urq7fZ+eXv469/XzsrFwb68ure1tLKysrS2uLq8v8TJztfkc1pNQzw3MS4rKikpKiwuMjc9RlNv39TO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Rz83LysqKSotLS0uMzxev7CqpaCfpa6yr66rq6usrq2trbG71U08MzQ2OTg2OkJi1crYWT81Ly8vLiwoJCMkKS40ODk8R+W6q6Ofnp+lr8DFwLSurKqtrK2us7zUTDw0OT9PWE9LSVBn9nBNPDMvLzI1NC8pJCEhJisyOTs9Sum6rKWgn6CmssbIw7GtqqqvsrWztrvPXj44Pkd+3eFyU0tPU19XQjgwLzAyMy8qJSAgIykwOD0+RWDDsKein5+jrsDP0bevqKissbi5u7vH2Ug8Oz9Y6M/WblFMS1NVTkM5NDAwMS8sJyEfHyQrMjs/Qk7euaukn56eqLnS5b+zqaeqsLq9vr2+yl9COj1Jes3L2lpHQkROU0xANzMxMzY1LykiHyAkKzM8P0VM3LqspJ+dn6i609/Dsqqmq6+6v8PFw83sT0RDTnjNytD3UUpHSEhEPjk0MjIzMi4qJSIhIyktNDg6RFu/rqafnZyirsHZy7yvqqmtsrzBxsnG2fdVS05Y5tTU3PVfWU1JQTw4MzIxMzIvKycjISMmLDA2NztL27asop2bnaavx9HPva+sq66zvMfN09vxblpcZOva2tnn+HBcUkhAOzUyMDEyMi8rJiMhIicrMDQ4PlXKt6uinJudo626ysW7sq6trrW9yNDd6HhjXV1o/+Tb297l/GVTST85NDEvLzAvLisoJSQlKCkrLS84RO3CrqKdm52gqrW/xb+6s7CvsrW7wsze/mdpb+7c1NPU1NPW2vFdSj43MS8uLi4tLCooKCgoJyYnKS41Pue4qKCdnaCnrbS6u7q3tbSztLW5vsjS3erq4Nra2dfRzs/W7GZSRTw2Mi8uLS0rKikpKiopJycqKiwwQtW1qKKgoqSnqqyusLO0tba3ubu+xMnN0NHU3OTn6+vr7fd1cF9QSEE9OTYyLy0rKiorKyopKysrKywxPW7AtK+trKupqKenp6mqrK6ytrq+wsXGx8zT2+Lt+fPw/G9oXlZQT0xGQDw3Mi8uLSsqKCgqKScmJyovPFDnzcG6tK6pp6SkpaaoqauusLS5vMDFy9Tb3ubt7eDf5Ojte3vw+GpWSkA7NzMwLSsoKCgmIyIiJCguOEBKYNnDtq6qp6alpqeoqKqsrrG3vL/GzdPY5H589Off3d/e4N7Y2uJ9WU1FPTo2LywqKikkISAhIycrLzQ5QlzRvLKtqqioqKiop6iqrK+0uLu9wMfN1uLl4t/b29va2NLOzs/U5nNhUEc/OTIuLSwoIyEhISQoKiwuND5U1r63sq6trKupqKeoq62vs7S1uLzF0Nne3tra3uDi2s3JyMfJyszP2fZZS0A4MzEuKiUhICAiJScnKSwzQnLLv7q2sq+tq6urrK6vsLK1tre7wMnNzMzOzc/W29jNw76/wsTCv7/Ezt9hSj85NjAsJyMhICAiIyQmKi44SPPOw7q2tbKur7CvsLKztLa5ury+xtDT0tXRzc/X18/Fvbu8vr67ubm9w83nV0Y+OjQtKSUiICEiIiMlKC43QVT20MW+urm4u7u5t7KwsbK0tbS0usTR5+nb09r9YHjSw7u4ur67tbCyu766ucpQOzk6NzMrJB8fISAhIScxRerV1dfOy9jiWk/5y7y5t7SurK60vcjLztbf2NPPzMnAvL7M9tm/ubm+urGusbi3t7fJTjw9PzQvLCkmJSMeHiArOTw+UdTMzmtNXmRiYenKuLC1t7e2ucLTzb7Dz9bMw8TR68y9vMzi2dXX48+5r7G4trKxus7TxsbtSEJDPS8pJyopHh8oMjYzPHDA209b9/xNP0Xm0dDCubS0u8bFxs/Z4dXHytbMxsvSzcjEwc7u/PxeYuvPwLy7urm9vsDIysfIzc/a51JGPDo4KicpLCkrLzNARktVcv32aUpMVF195NTDvcPEvbu8wMbCvcPfcubc82X4zMPN39fR+VRSZ/V69tHGxcbEwsTHzdnWzc3Kwr2/1l5eTTkxLy4uLS0wODxAR0xOUE9PU1di+uvm2s/JxsbFwsDBxcjHyM3U19bW2d3Z0dHU19jZ3ef4fXltaGtvcW5pa25paHP58Orm497b3+Xh4/VnW1ZTT0tJSElISEdKT1NWWmBpbnB49+/t6ubg29jX1tXV19nb3Nzb2tfU0c/NzMvLzM3P0tfb3+bv/3FraGRgXl1dXFpZWVlZWVhYWFdXV1hbXmJqePPo4d3a2dna293f5ez0fXBpY19dW1pZWVpbXF5hZmxxevvz7uzr6uvt7/T3/Xx4dHJvbWxramlnZWNjYmJiZGVmaGlrbW9ydnl8ff/9/Pv6+/v7+vj28e/t6+ro5+bl5OPj4+Tl5+nr7fD0+Pz+/359fX19fX18fHx8e3x8fX1+fv/9/Pv6+vr6+/3/fnt5d3Z2dnd3d3l6e31+/fv5+Pn5+/z8/f7+/359fX5+fv9+fXx6eHZ1c3Jxb21sa2ppZ2ZkY2JiY2RlZ2hqa21vc3p++/j39vPx7u3s7e7u7/Dw8PHx8vP19/f49/b29vb29fT09fb2+Pf29vX19fb3+Pf39fTy8vLx8e/u7evq6ejo6Onq6+vr6+zt7/P3/H16eXl7e3x7enl4eXl7fX5+fXt5dnRycHBxcXFwcHBwcHFzdHd4eXh2dHNycXJxcG9ta2ppaGdmZmZmZWVlZWZnaWprbW5vcnV3enx+/vz6+Pb08fHy8/X29vX09PTz8vLy8/P08/Ly9PX19vX19PX29vf3+Pj5+fr5+vv7+vr5+Pf4+fr7/Pz9/35+fn18e3x8fHt7enp4d3d3eHl4dnR0d3l5eHZ1dHV2dXR1d3h4eHl6d3RxcXN2dnNwcXR4ent5eHp8//z59/Pz+Pjz8fPz9/r28vLy8fHy8O7v7+3t7uzs7Orq6+zs7O3t7u7t7O3u7/Dz9vf7+/3+fXt4d3t6dXZ6e3l3dnl7enZ4d3V1d3h3eHp9e3l4eHNxcG53d25ubmxtbGxucXByev//eHVzbGdoXl9gZmhobGdlZWhbXlxr2+J8VHFU0/JEUHvbbt3PyOPbbWJcW2x6a17z5sfFv8LCw76/z9Zz5uPX3dnt6NvWy9bV3t3b4Obl7P5tfebtbGdfcfHh+WJjXnZ39/1tXF5hcWlZXltdUWBbV1JZeGhfTVJITUxSS0tLSkpNV0pKQUtNTUhGR0VKS1VQWFBYYmNbVWNo/W/2YnB27eHY7fHm3tjS1Phj6NHLztbd3drS1trp6NvP09XO1uDj0szL0tnk3+Xg1dLN1NXe6Pnb19re7OPZ3en5cO568ebX09nd7unk3P1mZF1iaOrm6+ja3Nvb3+11cGdu8N/0ZXDl4/rg19vo7fz2bW1jZfrq6nttce/ue2Nrc3RrbWJiX37udXPx8O7ycW527PltW2BeX2L78mtcX3f8/P9dTlNc/PJ9YV1YWWbv7m5XUlRfd2ljYGRlduz4aF9dX19jaWllYF1gcvTyfmxsd/J2dXdtbGxnbvba2ODw7eDc5GRZXmJrd/Dt7ujk5eXq6+vm7HlsbndtdeDZ4v5mYm7v6Ov7aHHq2dji929w7d7a43ZpeuHa3uh+cG/75ep2amdxefLn83v16d/j7XtscP3u6/pwaGh27ODo+nzw5eLtc2BdZn7m3+Z+aWj43dbc9Ghs8N7b3u5xbfrm4up6Yl5kd/X8a2Vpe+vm63dhXWBna2piX2f54uDrfXjz6/JwYV5jb/fw9/Xn3Nfb5e/t39nc62tjcezk72hfae7g7WNWWGj3/mRZWWV7dl9VUlZbWlNOTlNfbHh9fe3i397f39nTz83Nzs3LyMbGxsXEwsLDxcnQ3PBsY1xSTU1QWFxVUE5MS0c+OjY0NTU2OTxARENCQT8/Pj5ET27Uxbuzraqop6enqaywuMDN5GRQS0xUaubYzszO1+tnW01AOjY5Pj88Oz9NW1dUV2NmTz42MC4tLCwuNDo/RFPm087KxL66ubeyr66trKurra+2vcPSblRPU1xq3Me/vr/Ex898T0Q9Ojk6P0hQY+HOys3Qz9d4TUA8NzAuLS0uLzI2OTs/Q0ZJTVlt9tvJv7u3tK6pqayrq661wM3Rfk1OXevYz8K5ub2/xdNjRD49ODU2PERKVOfOzc3Jzd9iTEA7NTIwLy8xNDc5Ojs8PDw9P0RIT3HazMO7trKtqautrK22vMHR7F9YbuvozL+9u7q8vcTX61xGQD06PD5ASlhq3c/PztHa5mNORj46ODQyNDQyNTc3ODs7PD5BREhMVvff1sK4sK6uq6musq+4zszYXFx3fdnOxbu6u7e6xcfPXUpFPDg6OjxCS1r64NHJzdHR4mNXS0M8ODc2MzQ1NDQ3OTo7PT9CRUlMVF9yzr6+ua6usK2ssbS3vsfO3N3k6NXOzcK+v729wcXN6W9WRENGPT5LS0pi43ry0txz399cWF1NRERAPDo6OTc3OTc4Ojk6OztAPj1a+FvHt721q6+vrK+2trzFytfd2ejbzc7Kvr+/u77FxM5yaFhCQEI/P0ZKTFZrc/vf3eXk5f5eVlFJQD8+Ozk7Ojk5Ozg5PTg1Pz45R21R3b2+u6+vsa6vtbe6v8fO0tnr4tHTz8bDxcG/xc7O3GBXVkhFSUdFTE9OVmJscHTv7HF5/F5VUk1EQUE+PD8/PT48PD86OD06Nz9GQl3S0cG1tLKtrq+vsLe5vcbO0Nvp4tnc0srMy8XL1dLdZmFZTEtMSUlMTk9UW1pfbmxt/Hxxa15dVElKS0FASUA6RUQ1PEY1NUA5NkZJRvvMyry0s7Curq+vsba5vcLIz9fY2+Hd09fWzc/Y09x8cF5OTUtERUpHRk9STlhlXF/6/Gl+flxXU0pEQkA/PD1APTxCPzw+Pz0+QUdMXdrSxri4ua+utrGuubu2v8vByd3Pz+DWztvW0Njc3Ox9ZlJOS0A+QT4+SE9PZt3d2s3O2ufsak9KS0A7QD03Oz03OT47Oj9BQUdNVnD32crHwru6ure1t7e2uLu8vL/FxsXK0M3M0djT0d755/9ST09BPkA7OD9CQFD++dfHyMrLz+dcTkg9ODg3MzU4Nzg9QUJIUlhYb+jr49TR0MzIxcO/vLu7uLa4t7W3ubi6v8HEys/U2Ob69XhcXFxPSUhDPjs6Ojk5QE1LWM/K0sW/zN7hYEY+OzczMjQ0NDk+QEVRX2ju2tbUz8zMzMfGxsC+vrq4ube0tba2uLq7v8PHztfd7HNoYFtWVVNNS0tFQT88Ojw6OUFLR1Xa19rLydLfeVpMPzs7NzQ3OTg8Q0lRY/fc0tHNycvKxsfIxcPCv727urm4t7e4ury9wcjLz97y+nVnXl1dV09NS0Q+PTw6ODg7PD1EUl5w49bP0Nne611PTEU+Pj49PUBDSU9Zb+Pb1c3MzMvLy8rKycXCwL67ubm5uLi6vb6/xszO0dnh6vhtXVVOSURBPz08PDw9PT5BREVHS09PT1JYW1pbZm1ra3b2+W1qdm5dWFlZVlVbcfLp18rGxMC9vL2+vb7BxcfIy8/T0M/V19HP0tbX1dnpfXVfUk1KSEVAQERDQENJSklLT1NTUlRYVlFQUlFRUFBVW1teanx+9O3s7Ov28Ozu7OTh3NTU08zKy8rKycTIy8nGyczOzszQ3tvW3PJ+dPFyU1RjWktKTk9NR0pRUkpLUFhRTk1bWVJNV15bV1hvbnlq6eTa29va09HX1dfO09HU0NLP0dzW0tDh6NnW33j33uxsbPL4bmdhbmldW1piXlpQW19XUlFgXFpSWXNlXVv3dGxe/vflcvrx4uFq733c+X3s393r4/Dg6OXt4uDt6evk6e7p93rufOpw8fjzdfrue3f+9m76c+z5a2br62xt6+j+Y33ibGhn8mv9aH738G5ufvv1ZXD89Whv/vDz/Xz07vFv+fvsev759/n0a/Lu73Hx6PF0cPD4/X736vR5cOznZ3Jy8W1xZvrqZ2xj3mH+W+1sc2dtfPpqZ3V1fGv9a+77e3b89X10+PjjeO308nd7dut6dOV9935x+fd1ZfH4/HJo6mlvcvl1eG5r+mx0/vp8+2zsanp5cO1mfWr8dHJ39PR07HTq/eZ6enhz8nZ57nTvefnr6u//cfP2fnz543V3bet+72bu6e9pbvXs+GZ5e3n7Z3Xv8GZubvbraHj47HLvb+zw+Hpt8upx+/PvevX/e/n3+3v5/PtsfOLjanj26vt0effubvd98O31bPL26m198O9w//f75mJu9O9jd+7yZ3fu53By+W31/3l77fFscnzjfXt98PVxfex9+HHpdOJp73LgZ+t05Hz9+fL8ffl2ef5u8G32e+9+ZfhubWNvdm37d/ps8Wj7YfJn81/tdvxud/x8d3R29Wd8ZPltfXX29f1ue3hxbntmbPVjdmnx63dravpzfmvwd+32bvT3/Gd16XnqZtnmZvj03XJw7+Xkcv716/T5e23d7Hp15ej4+PPj7nfweupwfnB49/N0bufwfPX54/x6cPT28/d58PR6bPTs3fR9+PB7bvRy8GZxePXt+nlx93t+dGXwfG5p/fh0fWfsbfBp+HB08/d7/flufG3n/e9qbuRy7Gvt+v129Xtweu1u7Xd1d/BsfHV99nRuee97avhr6WR6Yd1n/mTw7nV6b+9z/WFybeRxdff95/1z8Ot2dXL/fulpfvbt9P15auxz/mn+9flqb3d593Fucux3bmp66/Jz/v76dXBtdfJ5a3xt7Pv8avHqeXVq+fVvcv7v/HB+dejv++994H3sbd5t42jj7d/v8ubx9P73+ut46vf/6uv9+/7obXN2/vpp7nbs//R5+vx67m12ePN2bnnw7nf1dPT+e11m9H5ubXv2a3Zjc/7ubWRu8XxnffvganV69nd17mnzcnBjZm36eG3ubfFubXF5dWt0Y216aOx19G7ubftgbHJkYmbkZntx5P598Or64Nfe9d7m5dza2tfa7ebc2Ovj9OJ1c21ve/5zZF5ZWlxTXWVnY3V37+bb3dna9m1s+/fq6ettX15YV19VUVNXW1BVXmxeX2Fy+Ot86d/W4eHk3+nv5OPt59/g3tvZ3NrT1tvZ0Nfe3dnd393c6/b9+GtqbmdeXmReXWBwa2VhZGNrbHdwc21xdHRxd/19d251fPV+cWpvcWtkZWlnZGdsa25wbWdrb3Nxd3hzb3N99/b+d3d5d3h9+ff6fn79/Pz18vd9ff738/P28+7u8O3q6uzs7O7v7u7w9Pb18PH+dHn7fnJz/PHy9vn6+/v/eXR2e3p3eXdxcHR1cW1rZmNocXh4ffr4/P38/Pbv8Pp9env88+7w9/57eXp8e//7/3p3ev358u7w+Pv48vDw8ff7+/r49O/t7/b6+PLu7vDv8Ph8eP728/L1+vj18vHy8O/x9Pf39fb7fXl6e/3z8PP5/nt3ef77/n54b21wd3d0c3FvcHBub3Z7eXR0d3z8/Ht3dnRwcHV2cG5ua2dkY2VqbGplZGhtdvvw7e7z9/Pv7u7v8vh9en3/fnp2dXFucHb98e7w8/Tv7ezs8Pv9+PT09PP6enZzcHF3fn56d3N0fvXy+nh2eXt+/n18/fp+eHp+fHp+/3dzev5+dnJ0cnB0d3h7fn57//X1+vj4/nl5fnx4fvr59vLy9/bw9H787+zzfv/9+vPv9f798/Hy7/P8/fDs8n1ybGpsdHdwcG5pZWVpampvb2hjY2lub3Jybm5vb29xdHN0fvj+dHN9+fb2/H18ff//+vf6/vv08fX59/Dr7PD19/Tv7ezt8n11efn08/P4/3z68+/s7fT8+vLy+fz9/Px+e3h7fHl5eX3/fv39/v1+/fv8+vfz8/p9fPz4+3x2c3J0eXl1cXB1eHl8ffv5/Xx6fvr6/Ht1cHB2d3BqaGlsbnN1cW1tb3R5fvv08fL09vX18uzq6/D7e3l7fnt1cm9ucXp9eXZ7/n5+/f38+fn7fXz//PXy9Pn9/fr6/P39+vTw8PZ9eHv//Px9cmxsb29vbmxrbnR3dXV4ev/29Pf9//36+fv+e3ZxcHNzc3V2dXV3eXz58/b59/f9fvv5/X3//f79/Xx6ent7fP58dXF0e35+fn1+/vv17+7w8vDw8vP09vT1+3789PP18/T18u7s7Ovr7u/u7vLz8fL3/Pz8+vf4/n79+fn7/Hx6ff98eHZ1dHR0cXBxcnN0dnZ3e/39/vz28vL09/j39PX3+v1+fH3/fX19fv99fH19fn57ent7ff76+Pr6+Pf18vL2+/3/fXx8d3NydHh8fXt4ev738Ozq7O7v7uzp5+jq7e7u7uzq6+vt7/H08/Hv7u/1+/r49fb3+v3+/fz9/f39+/r8fHd2dnh5d3Fsa21vcXRzcG5vc3d3dnVycG9vb25ubW5ubm1tb3N4enx8eXd5fP77/Xx5en3+/Pz/e3h3d3d0cW5sbW9wb25vcHJ1e359enl6ff39/3p3dXd7fn58eXh6fvv6/Xx5eHh7fn17enp8/vr39vb39vb3+fx+fX18eXVycXN3e3x6dnZ5ffz6+v19e3p7fX58eXVycnN1eHp5d3d4fP/7+Pb18/Ly8fDu7u7t7e7v7u7u7e7v8PHw8PH09/n7/P58eXV0dXh5eXd4e/749PHx8PDw7+/w8vT4+/5+fX78+vx+fX3+/Pn5+/z8+vj4+Pn5+Pb2+fz/fn18e3h2dHR2enx8e3x++vb29vb29PLy9Pn+e3l5dnJvbWxsbW5ubm1sbG5xdHRzc3R2d3h4dnV1eHl3dHR1dnd3dXNycnR3eXd1c3V4e3x8eXZ2eX79/X58ent9/vz8/318fX5+/v39/n58fH1+/Pv8/n59ff78+fj4+Pj49/b19PPz9ff6/Pz7+vr8/3x7e3z+/Pr59/Xy7+7s7Ovq6enq6+3t7e3u8fX6/f78+/r6+vv8+/r4+fn6+/r49/j6/P7+/v58eXVzc3R0dXV1dnd5e339/Pr6+/39/Pv8/3x4d3Z3eHd1dXR1dnd4eHh4eXh4eHh4eHd2dHRzc3NycXBwcXJzdHV1dnd4eHd3d3Z1dHR1dXV0c3R1dnZ3d3d4ent8ff/9+/j39/b08e/u7e3u7e3s7Ozs7vDy9Pb29/j6+vv6+Pf29vb19PT09fb5+/z+fnx4dHJxcHBwcHBwc3R1d3l8ff78/Pz8/P39/v5+fnx7enp7fX5+/fv6+Pf4+Pj4+v1+end2dHFwcHBwcHBwcHJ0dXZ4en3//v39/Pv7/P5+fXx6eXd2dnZ2dXR0dHV3eXp6enl5enx9fXx6eHh5e3x8fHt7ff77+vv8/P37+vr8/f7+/fv6+vv8/f36+Pf39/j49vb39/f39/f3+fn7+/r6+fr8/v79+/j29/f4+Pf19PT19vf3+fr8/v5+fXx7enp7e31+fn59fv/+/P3+fnt5eXl5eXd0c3NzdHNzc3R0dnd4eXp6e3t7fHx9fXx7eXl5enp7e3t6e3t8fX1+fn7//v39/Pv7+/v7+vr6+vv7+/v8/Pz8/P39/f38/Pv7+vn4+Pj39vX08/Ly8fDw8PDw7/Dw8PHy8vT09vf2+P31+Hr6+H539vxx//50efV1cn58eH5+enhx93Z0/XR3e3t7enl8e3x9eXl5eHl4d3d2dnZ3eHh4eXp8fX1+//7+/f38/P39/f7/fn18e3l5eHd3d3d3d3d4eHl6enp7e3x9fX7//v38+/v7+vr6+fn4+Pf39/b19fX19fX19PT09PT09fX19fX19vf3+Pj4+Pf3+Pn4+fn4+fr7/P3+////fn59fX19fHx8fHx8fHx8fX7+/v39/v7+/v3+/35+fX18e3t6eXl5eXl5eXp6eXl5eXp6enp5eXh5ent7e3p5eXh5enp6enl5eHh4eXl4d3Z2d3h5e3t8ff78+/r5+Pj49/f39/b39/j6+/z8/Pz8/f7+/v39/f39/f3/fn18fHt5d3Z1dHJwb25vcHFycnJyc3V2dnd3d3d3d3d4eXl4eHh5e3x+fv////7+/f38/P39/fz8/Pz8/P3+/v7/fn16eXh2dnZ1dHNzcnJyc3N0dXZ3eHp7fH7//v38/Pv7+vv7+/r49/b29/f49/f29vb29vX19PT19fb29/b19fX19PPy8fHx8vP19/j5+vr8/f7+/v79/f39/f39/fz8/P3+/n5+fn5+fn5+fn5+fn5+fv79/Pv6+ff29fX19vb3+Pn6/P3/fXx7e3t7fH19fX59fX18fHx7e3x8fHx6enl4eHd3d3d4eHl6enp6e3x7fH7/fXt7ffv5+/j1/fr3", "UklGRmRAAABXQVZFZm10IBIAAAAHAAEAQB8AAEAfAAABAAgAAABmYWN0BAAAABBAAABMSVNUGgAAAElORk9JU0ZUDQAAAExhdmY2MS43LjEwMAAAZGF0YRBAAAD+/v7+/v7+//9+fn5+fn59fX19fX18fXx8fHx8fHx8fHx8fHx8fHx8fHx8fHx8fHt8fHx8fHx8fHx8fX19fn19fX19fn5+fn7+/v7+/v7+/v38/Pz8+/v7+/3+///+/Pv7/P99fv77+ff29vb19vb3+Pj39PHv7u/z+f98fP/9/P7+/fz9fXdzcnd+/P91bWlpa2xsa2psbnBuaWpt/evo7XNoZW3+93tpX2Bq9evtcGNcY2p+bGZdX/zn3OZ1bV5re/fNx7u9zXtITF/dyszT3fXb3NrsW1ZNVmH04fxbS0RERkpIREJHXvvcZVRQ7Lqtqq++3fbNvrrD1mzlxbi1vNhQQ0hSYGNaV1ZQRTkwLS41QE9SRjoyMDAyNTg9R23Lv7rBv76xqKWostNbTNm/t7O6usDFy/5YR0RSas7My87lZUM4MC0vND1GRkA8PD07NzAtLjVBWfxkb+G4q6Oip664vr7AwcbFvrq0trrG4F1PT1hbbnD5bltLPjcyLy8wMjM1Oj0+OzQvLjI6SXHe0868rqWfn6SrtsLLzcfEvr27vL7EzvpURT8+QEZKS0g/Ozc0MzExLy8xNTo+Pj05Njc8SV/n182+sKihn5+iqK+7yNbg39zQzMnIyszX9FhIQT07Ojg3NjY3ODg3NTQ0NTc5Ojk5ODpATW7azsW9squloaChpaqvuMDM2erv9/Hq3dfW2+xhT0U+OTUxMDE0Njg5OTk5Ojs6Ojg1NDY7RFX73c/Fua6oo6CfoaWqr7i/zuZnWlVUXHLg1dTa91pJPjkzMC8vMTQ3Ojw/QkRBPjk0MC8xOD9MWHDcxbaspaGfn6Kmq7G7x9twWVJPUl7329HQ1+1cSj86NTMxMTM0Njk8QENDQD06NjQzNTo/SVrzy7yvqaShn6ChpKmwvddbSUVESE1ZbuXc2+V4XE9HPzo1MjAyNTtAR0hFPzs6ODg3Nzg6QE3rxriuqqWjoaKipKetuM5aQj09REtWX3D/7PD+X1FHPzo2MjAwNDg/R05MSEA8OTg4OTk7Pkv+xriuqqakoqKhoqSpr8FpPzg1OD1ETFdm7NzX3XhURTw3Mi8uLjE3PklOTkg/Ozg4ODo8P0hfzbmuqaako6OkpaeprrfWSzgzNDk+RkpPVF9tc19NPzgzLy4vMjlBU+/VzdDbcVFFPTo4ODtDYMu4rqmmpaWmqauvtr7mSTcwLS80PEJLVvjRxcDCzfdPRD8+Pz9AQUZOX3v2YlBFPjw8PT9ES1nqyr22s7S3ury9vL26vsjW6NjMwr/DzNnyZk1ANjAuLzM8RFBl69vU3mI/MiwpKiwuMTY/XMq3rqahn5+ip6yvtbi+yGQ6MDI85sHAzGx32+BOLyMfHiUuOkFFUHXSxc/+V9+wpaWs10tDU/FYRUJotaejqr9VPUJj4+ReSURAQD85MzA0P17lYUc+S8yroKCz0ztK6V5HLjBatqew9TEpMjg8NTBFxq+y4DktMT9IUl7RtrOzv9K5p5yivUw0zb3QQSo6wausUC8pMj82LzBHvrjIOysqLS4vM0zMucDb0sSqnpqct81LybteOTJMs660TTExODMtKDfKsLJuNS8tLikmMGCwr7ve37yto52artVZT7lYODJEtbLFRCszMC0nJTy7rbRcQTs2KyMmNsmwsL3Qybi2qaCWofVaQ7bGOC86vq7QPy0zPC4mJTO4rbR4Pjw5KiElOcCvt7zOyr7FraCWnWFOQLa7Oiw6w6zKNi0wOi0gJja4rr/0TEw/JyAmPLqvurjAxr/SrJ2XnE9AWLSxPSs92q7qMi4zPi0fIza4rrz1SUY3JiEpSbmxxb/Dzr7Wq5qXpjVOxqm6Ly9OvrI/MzY3OCMdKd6rq8ZLOz4zJiMs97C1wsXcvb69oJeb5TnMtaNDMDhZsH4wNDJEKxweN6+mtFM5PkQsIyU6tq69xc29v8ellpm6P8m4qOItUkix7jY3NTMoHCT7p6fUOzpGNyUkLHa0usPCycnJsJ2aodLWsrZhNTvGuN5aLzYuHx8wv63L3PLNOyEfK0TKaEy+r6e3w7umn6W7wLq+fj9NzLrJOykjHiI3pKhKQSzBxy0gIC6+wdXjv6qrv72voJ6wzcu+wUk9UM7MPSshHSEupJ/LSShazTIhIC65tr3kwaqquMq8p6GqwNjP0FQ2M0bfVDAeHB1Fnqm5LDHh3TIgH0axqbnouqalrsa9p6Sqv1pYXkY3M0BMRSYfGiapoKw8Lki/QSgfLbmorczoq6Wntsitp6i26VZXUD03PDw/KCAaJKyeqFoqP89iNCQswqimw2u8q6Ovvb2uqq/XPjs9Pj03Ny0iHBo7oaSqOzja0/ovJ0C0pKnOzbypp7PRybustk4zLTVDQjYmGxkd/KOurkrPumxFLDDFraatx7Wxqq/kW82xqsE3KSk+WEQoGxkiUaq4t7u3q2EzKDS5qa22z7Cop7BNOV60p7Y4JiY36UwqGhgj/a23srSutEsvLka0rK+8vK2nq8Y6OM6tqt0uJCtDSjAeGR4yx7Ovpa67SCtCWq6vtr27q6arwzs11K2pzi8lKz1FMiEcIDLIr6igtNAxLlXLrbG5vreqpa3UNTrUrrF2LiYpLzcuIh8jNryjmqbWLilhvqmwv9C8qqSr0jo4/bm48DInJisxLSYiJT2znpyvUScz+7CotMTNtKelsPc5Quu+zUYuKCkrKyglJji+n52q6Sw0SbOqrrzLuqyorc0+Okbd40wxKSYoKiopLESyn6OuPzAz6q6prb7Gu62rsdhFOkVXWz8vKCUlKCsxS7OjprJIODfesaqrs7m6s7C1yF4/Pj09OC8pJCQmKz6/p6at3Ts3Rr2tqq20urq1tLvZTTs5NzUwKyclJis8wqelrNE+NkLCrqirr7q9vbm9zlg9NzMyLysoJycsPrqnpazTQzZKxa6pqa+4wMG+x9lLOzIvLSwqKSgqMXitpqa3dTw8frqsqauwusDByNBgQzcvLCknKCkrNu6tpqe29D07YLysqKmvucnO4+xUQTgvKyglJigtPsKppqm/VDk9eLqtqaqvuMHN6l1EOjAsKCUmKSw3ZrKpqLHKUUNXzLWurK6yu8jb/WJLOy4nISImLT3gt6+vt8LX2s/Evbe0sbC0vMvfdFpEMygiICUrOnjDv8LFxb+6tba3u7u6t7q+ydTjaEw5LCcoKS04VdzZ5enVwLWxr7GytLKwsrm+x83bWz8wKSgoKS44QERGS2bJtrCur6+vrq2utLvBx85vRjMqKCgoLDM7PD0/TOG+trGwsK+uq6uvtr7DyuFMNysoJyYoLDE1Njg9WMu5sq+urq2rqauvt7u+yPVBLyknJicrLzM0NTZDecO4s7GxsK2pqayzubu/z1o5LCclJSktMTIzMztS0L23trSzr6uoqq60t7i/0k01KygmKCsvMTMzN0fpw7q4tbSyrKmprbG0trzKZz0uKSUlKS0vMjM2P13Mvbq3tbSuqairr7S1u8boRzIrJiUoLC8zNTc+UdO+ubi2tbCrqKmssbW5w9hWOS0oJCUqLjA0NjlDbse8uri2sqynp6qusba8yfNAMCklJSktLzI0OD5W0sG/vLm0rainqayvs7rE20o1LCclKCotMDI0OUVvzcS9ubSuqaamqq2xusbeTTkuKCYoKy0xMzU4QmLRxb65s62npKWprLC5xt5LNiwnJScrLTAzNThAWuLRysC4rqejpKmtsLa/0VU7LikoKy4wMjQ4PEdo4NzTwLCnoZ+jqayvvW8/NC4qJCInLTU5PUtp7tDO1s6+rqSjoqeusbxKLy4wLyolKjg5NjpL6mhM3cvLxruso6akpbG85TQrLCkoKSgzOTM+TlrwXOu8ycm3r6qnqKKnvcHmMCwvKCkvLDg5OFRFSthU78DRwLW0qamopazGwkUrLS0iKzIpOkI1ZVFOzmfQu822sLKmqqqms8vUOCktKiAzLipTPjvOSm7Jbr+7xa60r6Wtqai8zP0wKC0mIDgsLHU4QspA28hxuLm8rLOsqK+qrcTbSCwoKiUjOi0z5DxQyEbOyNi3trStr6qqrqyx2Wg+KCgrIik+K0B4OuPcSsrRy7W0sKysq6qur7VmYTklLSgiMTMsXURA1FBXzPzCtrStrauqrK6xuVZUNiUtKCM0My1cPz7eRkvPbsCzsKqqqaitr7TCTFAuJy4mKDYyNVc/RXpAV+H6ubSupqulprKstuZgQSsrLCYtNTM9TT1NUjpgWmW2uqylrqKot6u9dWI7LCwsKC85MkdJPWRBQGtGy7i7pamsnq+vqs7wXDQrLiomNjIzUjxCYzpNXUm8u7WirqeguKywZGxLLCsvJSs5LT9MOF1KO2hKfbm+qqWvoKe7qr5M8zkoLysjNC8zSz9LWUFQSlnBxa6nraSis6uv8OFSLS4vJy0xNDs9VEJHWTxMy9m1qaqnoquvrsP/WDItKyktKT48MdVAOds6PchrvKqsqKKor6255203LCsqKidBOzPJRzvVQTzM7cKsrKqjqKyrt8tlNiwoKiYnQDI4zz5E2D9Hydq/ra2qpqisrLXJVS8uKSIrLTBBXUlYXkhJVGzLurSrqKqnqK6xv1UuLC8eJz8rPMg/Te1ASU1owbuwqamppquus8lMKyovHSpFLEnIQVRfQktF37q+raarqKessrzTPSQtKx0xPjJe0E5PRUw/PMi+vqimqqaorLbHzy0jMiMgNzo/eN18PkdTN0K8vLOlpKemq6/HzVciKS4fKT0/WO/LWjlMRzNhu7ispKSnqqy8ds4uHysnIS1BX17Vw0c6WTw51Lmup6OhqKut4M5aIyQrJSYwXvVcxck8Okc6Pc2zqKahoquryOXJLB8oJyUpPs9q7LxiNzs+P1TAqqWkoaanvWC+USMjKiklLdfOVsLEQzY4RUZZr6akoKakrmPGzy0jJisoJkfEftzIez0vPFBKwKqjoailqc7XwEQoISktJTHUy97a1k0yNElH5bKjoaajpr3JvnwuJCgpJSxG2eB5zV47NjdEX7+mo6efpbm+uL81Ji4qIyg5ZPhyw+w7OTU9Tuispaieorm4srdPKS0vISArPVtM1r5MOC0xVmW/p6mknqu4tbXDNyorKCMjL1ptW8vFZjwuPO3Ut62qn6Ovr6+0zTUrJiIhIi9DS1nTzOpGOEnQxbWtqKCmrq+ws9w0LicgHyIuOjxQz83gV0d8v7qyrKeiqbOvrbbsPDQpHx4iKi41RuzT5l1a3MC7tK2ppqiurq20xFlALyUhISYrLTdF/tRcas2/ub65rqurrrKurrnSVD0uJiImLC4zQGfj/djNy8W+u7i2sK2urq6us7/VTzYtKCkrKzI7SE9c1M7Lw765v8S8trS3uLK2wexPRjkwLC4wMDQ3PUVNW+zh1srCycy9uLq5uLa7xMnZZV5GP0E4Ojo7QkNEVU5cd2jf1t3OyMPDxr/Gx8zJztHqYmBuTVZTRlpYSUpMU1ddauve+Nrayc7J3tTP6ezPy9/qduZpXltp6GddYHZrXlzd7v549f7c4PTX+OBtamhifepYWWprbVf0W19tbFltdk5YYPJiWXDi7F5n/WBWXe9dWW//6m9r/Hp88uTt4HB3fnXkcHV1VnP9YWz3ZWVdbV9wce533d5heWrfbWNo7ujl5W5qevb13N9pbv7Z397jbXjo8+Vv9erscOnhdXDr8Xrpc+Tea1/o22x+3t7xY+bXcGl1fHT6Z/3q8295dX3zZ2x7cl9t+3p7+Pj/6fZs/W99ePF8Z3jtXHb492586+lraHZu/PD46Px4a+XmYXJzd21wbvjvZGV13151X/VcZvx1cG1dbHlpd3BvX3TxeGVpfGxsanrgc/D4fnBocuptfeR4+/xo9+1uX/H182xk62Np8v1wd2ln8Gd29Pj3fmznZP33evFhbmp3c2V66flx7v/p9N7zfW5p7fXz723wePjm4+Z+ZXH07vfu6314d/Z67X7q53dgevHl9ml8bWbpcXX2bmlwcPzxbH3s8m7xeunr9/1p/+l+5eZwbvbwenF79Pj5bXl0et3gbHr+7/B5b3v1bfD7/u/3dPD472vz4fFo9/f95mFh9e1jbur7YHzj6Wl59GL473726fplb//d+Hhw+/h49uttb//neudxbv3na+7y6319+vF9+ft0dnxs+nj1fvDwZnxzamVye2l4+nR28HJwY/1x/mHt+3xuevv6dnR782R3antubfHt7Xdt7v5qau5rbHhhfmr75/RsX3zt7W3//ebwbPfzdl5v6fj2ZtjoYfd94Hdq6eTwcu/18nD072nl7fvy6Hv16vHf9PzncO9783hn/uN0dOp9derr4nJsefTy/fDq+fpyb/fz3+n0+XVo9Otu+mFz9H3u6/1teHDm/WPyfWho8vRu+WXucfNw829h5Or7+HhrcnHl7u1iY+Dx8G/v9Xn67n5k/vFv6PBmdvlr73N3+Xd0eO97afh58WlsaN5tbGzr92n47vdxaF55cOxq9/5q7PH+53dmd/N8a+l3dvvu8O9zX/n18HTz7HlhfnZsdXV78ulvcm/w7evw7Hn6b3d1Z/p2ZXxj8uZ2ZezofP1x+G9o7uH3YWR5fubv+/N16vPj6+Nd+GPf6Nfk8uby6d94bOn33vRt2N/v9/PdY2ryfGle4Oru/nP75W9p4WtsYPF5aGj7bu3de/z851Jcd337dH3xeG5SaN33WmB17llb2Ob7Yfnb5lp92WTueF/uXVTtd/ngY+J9X+ZwYv5oW25lauvp+Xh16P/xbnZiXVfxWF1fZ2RgX/FeXt3e+99haPdaX/9vbmBi2N736+js/m1semtf8N7odOva2uB93NvweGBg32Ze5u97dltkaVZn9Wrb229+ef3gaF/w4+rv3svM1OPl0c/9YFdYYlRr0Njs91tORDs2NTUzLzA5PDs+T9K+ta2trq60v7+2tbKwvMCzr7G+RSwfFxQTFBolOcSqoZ+hpKi+SjXruEs5vKiipK2tp6ivxmVPSEZBNDcqGBQXHS0586Wlr6mv6jAjLEI5VLWqpaisq7xYVXjJucPDsqutwlg/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NDMzNTk/TGXayL66ubm7vsXN1+hwXlhVU1RXXGV58Ovo6Ofk39nV0tDPz8/Ny8nGw8HAv8DAwsfN2O5eTUY/PTo4Nzc4ODo8P0VLUV1z6t3UzcrJysrKzM/U2eH0aFtVUlFRVFhcX2RsdnpzaV9bWFdaYXXs4NvW0c7MzMzN0dzseGVaUk1LSUlKTlt44NLKxMC/vr6/wMTHys7U3OX2bmNcV1ZWVlldYmlucnFuaWNeXFpYVlZXWVxhanb78u7s7e/y+H11b2tpaGdnaGhqbXF6+/Lu7e3v8/l9dW5qZ2Vmam10//Xu6ufl4uDf3t7e3t7f3+Lm7PZ3a2NeW1hWVVVVVldZW15hZmpudX738O3r6unq6uvs7e7w8vb5+vz9//////78+vn39vX09PPz8/T09vj5+vz9/v9+fn5+//79+/r49fTx7+7t7Ozr6+vs7e/y9vv/fHl3dHNycHBxcnN0dXV2dXR0dHNycnNyc3Nzc3R1dnZ3eXl6enl4eHd3dnZ1dHNycG9vbm1tbW1tbW1tbm9wcXR2eHl8ff78+/n39fPy8O/u7u7u7u/v8PDx8vP09fX29/j5+fr6+/v7+/v7+vv6+vr7+/v7+/v7+/v6+vr6+vv7/P3+/359fHx8e3p6ent6eXl5eXh3d3Z3d3d3d3d5enp5eHl5enp6ent8fHx8fH19fHp5eXp6eXh4ent8fHx9//79/Pv6+fr7+/j29/j6+Pb29vf39/X19PLx8PHx8PDv7u/x8vL09fT19vT19vf4+vv9fnt7e3t4dnR1eXl3fP38+vv7+Pb29/X19fTz9fn38/H5/X59eHNzdv1ybW9tbGxvbnFxc3p9+nhwb2phZmhTYmR+Y2xqaWlhb0vwTNTAV0xO1FS+Zz/m6/reyLVXd+BTXez21VBGWOjUwca4t7e8t89OXlBSSFNTalJn287MWdnW91xYZd5MXUfjVlhE2PnnXdjodlrv3dZl2XXP7eDm2V93UXpF7lxp6nrT6nhR9kheSFlJT1RQSVZpSVRGX1BOTU5iTEpSbGddVeZw5VDre9Bd4dFl6Nvc6slr3M/e1NfQ3N14yuDO0c7b59Dv0l/05dLmadTe0Gbj39jf9G7f5EjXWstX213JR+VaZN3sQ9vv4HlJatZbS2ba1uNT3NndbU3r+G5C/vLOU+fWyej6cs/f90jg2s9MXt/JVFjSyHTmX87lb0192uV+VvTf4mJtX91mW17Ya/5S/dVpV+fd7Otk4engXOld7flsVV9wXGpe4m1w9NxJX1Xd+m1N3GNSTGDd0FddcGN1T1T+199tbfjneG1v6OzuXWFzeltg9ObY6nh+z9vofXjb3lxjXNnG3m/c1MrRVVRraXpRW+bq9PFg3tvzcPRqcF9bZ2Be7dPW/2FcfeppWnvy19PU197m3efwefp1d1pd/+V0Y1ZgXk9TVFNhUFdVTFJZV/riy8fP1NjMxMfIysi/vr6+vru6u77M6VhNTEc+PTw8OzYyLy81OTc3NzpFS0xZX9jGvriyrqqqqKShnp+mrLnMcUQ8Ojk+Q0A/Pj49ODItKSgoKSstLzQ1Nzk6PkRLW/nKuK6pp6WgnJuep7ngVkg+OTU6Rmne4fpaUEc8MywpKiwxNTc5Oz5APTo8P0VOWt7BtKyopqOfnJugrM1MQj8+OjU7RPjLyM3oXVFBOS4rKy00PkFAPjw+PTw7Oz0/SmvMuq+qpqSgnZudqbpJOTo4OzYwNT51v7y7zWlMPzk0LSwrMDtCSUQ+Ozk8PTw8OTtI9L6yrammoZ6bm6Kw5jw7OTw5MjI3SdC/uLzL60pAOzQwLi4yNz9FRkE9PD5AQkA9P0loybqxrKeinpydoa3AXkNDOjkyLzI5T9PCu8DN8E5HPzs3MjAvMjg7Pj49P0FGR0ZHR1Bw0L61rKeinp6fpa69315GPDUvLi83Rm7LwMLG23NWTEY9NzIuLzAzNzg6PD9CQkZGSU9c4Mq6r6qkoJ6fpKq0v8xmTDoyLy4yO0rfzcPGz9t0aF1NRTo0Ly4vLzI1Nzs9P0JESk5b+NbCuK6oo5+foqats7zJ6Eo7My8wND5R68zLzNPl5/xuWUY8NTAvLzAyNTg6PDw9P0FHT2PaxbiuqKKfn6Knq7C4wdtSPTUxMDY9THLe1tzr83v0el5OQTo1MjEyNTc5Ojo7Ozw+QktZ3sm6r6mjoaCipamtsrvKdUU6NDI1OkFNWV5aVFNUWl5cVEpCPDk4ODk6Ozs7Ojs8PUFFT2rXwLesp6OhoqOnqq2zu8xnRTo2NDY6PkJEQUA/QURKT1JQS0Q/Pj4/QUJAPTw6Ozw9P0FLX9a+s6umo6KjpKeqrLG6ym1GOzk4OTs8PDw7Ozo9QElPUlBKRkRFSEpLSUQ/PTw9PT5ARE5uzLuwq6elpKWmqKqssLrIdkw/PDs6Ojo6Ojg3Nzo9Q0lLSkhHSEtPU1RQS0dDQD8+Pj5ASFrbwrauqqenpqepqq2wuMTcWUk/PTs6OTk2NDIxNTk+RElMTU9VX3vq3+X3ZldPSkhGRUdMXN/GurGsqqmoqaqrrbG4wdNpTkQ+Ozk4NjMvLi4vMzk9QUZJTldo5NbPz9fkcF5WUE5MTU9c7c2+t7CurKusrK2usbe9yNptUklAPjo3Mi4tLC0vMzk8P0RLW/vUy8fGzNDe8HRhXldWWF/w1MS8trGvrq6ur7C0t73EzuFkU0hDPTo0LywrLC0vMjY5PkRRa9fKw7/CxcvP197j/Hlqde3Zy8G8uLWzs7Kztbe5vcHK1fZeT0hBOzUvLSsrLC0vMjU5PUZX7s/HwsXFysvNztHZ4e7t5dfMxb67uLa1tLS1t7i7vsTM3XJYTEY+NzAtKysrLC4vMjY7Q1Ptz8bBwMLExsjJzNDX5erf1c3Hwb67uLa1tba4urq+wsradVVRSj43Mi0sKywsLjAxNztDUO7NxsDAv77Av77CyMzP1M7MzsXEwb+9ubi4uLm8vby+y9RtZ0paSjYsKScmKCorLS4vN03cxre2usDBvr6+wsvR3NHMy8fAvLi1sK6vsbO1ubi3wNJL52ZVOSccGRkdIyw0OkzLtKikp63CXkE7PDk5PEnQtqyprK6vr62vtLzL1ce8sK+5yUzg7Vs0HhUSEx4rQ2F1xrCooqq8Si8tLzQ6OkXetqeio6q3y9Tby8zNy8i6raqkr7hPVuJORCMYFBMcLD3gVNi8raSpukwuLjA3PTtK17Kno6Wuv9lV/t3Mw8C8sa2loqqzQklQXkgpGhYUHSxNzdvPvLCorsg+LC00PEJATcCso6Suvehp+2V43Mm3r6ypqaStuVs5/z08JxsWFhszUb7F28K1rq7nNyopOEhfcXO8q6amssVXSlRs17+8s7CsqKanr9NcP207LCIWFhkkWL+/v3K9tbG8PispLUt6287Lsamnqrv3SkRny7+5u7mvqaOlrsRbTkk/KCIXFBskZb6+ytfDrrW7PywrMEbl7dPFtqmoq7fXVVnlwLu6u7quqKSnssl0VUQxJBsXFh8vVcZu3Mm2rrZuOy00PlFtaNq7r6ipsLzP0MjAvr/Bu7Gqp6qwvs9+SjElHRkYGyc7SnNS4rmxsL9QPTtBWE9Yasy1raytt7q8u7q9wcTBubKurrS90V5EMygfGxodJC89Q0ZZz7qzvddMRE914t/sz7+yra2wt7y6uLe6wcfHvrm5vs54XE5EOS4pJSQmKS0yOUBObNfLxsfLz9LPzcvKysjCvru7u7y9vb3AxszQz87O1ulxbP/w/l9OR0VFRUM/Pj5CSU9UVFNYYXrw8Pf06NvU0dDQz83Nzc/R0tTV2d/o7e3u9HlsZmRkYl5aV1ZXWFZUUlNWWVxdXV5gZWpraWZlZ2twdXr88Onj397f3+Dh4+bq7vDw7+7v8fPz8e/u7vDy8/T09PT09PPy7+/v8PL09PT19vf39fLv7e3s7O3u7/L2+/5+fX18fHx8fX7/fnx6eHd2dXR0dHV2eHp7fH79+vby8O7t7Ovr6+3u8PL09/r8/n59fHp3dHFvbWxramppamtsbG1ub29vb25vb3Bxc3Z5fP77+ff29fb3+Pr7/f7/fn59fHx6eXd1c3Fwb25ubW5ubm9wcHFycnNzc3R0dnd5e33//fv6+Pj4+Pj4+Pn6+vr6+vr7+/z9/n59fHt5eHh3d3Z3d3d3d3d4eHh4eXl6e3x9fv79/Pv7+/v7/Pz9/v9+fHx7eXh3dXRzcXBvbm5tbWxsbGxsbGxsbG1tbW5ubm9wcXJ0dXd4eHl5eXl5eXl5eXl5eHh3d3Z1dXNzcnFwcHBvb29wb29vb29wcHBwcHFyc3N0dXZ3d3h5eXl6enp6e3t7fHx7e3t6enp6eXh4eHh3eHd3eHh5eXl5eXl5eXp5ent7e3x9fX19fX19fn5+fn7////+//9+fX1+fn58fHt7fH18fX5+//38+fj3+Pn39PT09vXy8fDv8PDx8fDu7ezr7Ozr6urq6ejr7vHv7vP19e/s6Ofp7fZ8dXn79nZsYmVz9v9lZHnt9HJncPHwfWxlZ3fo5nNkfuXtcmprYlZZ7tfYbEY/UdnZYktPVGjMvcVQNDB3v0EqLvCzschFQc+/ZDQ4xauwTC01zrO/SThD6cS9xO9f7dbQ5Xbx4+L9/9rQycLAxcbI0Nbc725gY3RvaHLo3fNz8ezmelxjaXBubeTdemFq9tnQ3HNs89va7/Du5eTy6vB5dX1qWV9eWltbZmpnW1pZUFNVU09PU05PVlNTT05TU1NSUFJSUVVcX1laZmxoY256ffXz/P3v7+ze5urf397a19vk3dfW2Nzb29rZ3d7f393b3uLc3ODf397d3+Hi4OPn4+Lj5efn6e3o6PLs7fDr8PDw8vDy8fXu7u7v9vPw8PX6+v75+Pb1/v729vn7/f/+/f99fX16dnt9end3eXd3cXBxbm9tbW1sa2lqaWppZmZnaGdnZmZnaGlpaWtsbW1ubm5wb25ubm9vb25ubm9vcHBxcHJxb3FxcnFwb29xcHBwcHFxcnJydHR1dXV1dXV0dXV1dXR0dXZ3d3Z3ent8fH1+/vv7+fn59/b29/b29fT29vb19fj29fb2+Pn6+fn6+vv8/fz8/n5+fX59e3t6enp9fn7+/vz6+vn4+Pf19PX19fXy8vLz9PPz8/L09vX29fj6+fr5+vv6/Pv5+Pr7+fj5+v38/Pz5/P5+/vf6+/r39fn69vb19PLw8fLu7vDu7Ovt8O7t7O7y8vTz+vj6//x8e29sbnFvbHB6dXF0dnNucHFzcXt+df///Hx2eHz7bmxtZGNfYGllYF5kamdranB+c3Ztam1tdXFt/Ph29+Hj/2387ejx/PDv5eptaG5rYV5aWVxYVltn8et8bnR7dfbn6erh3d/j3dDJwr/Av72+wsrV5P9dUVFTTUU8PkhGPTs6PkdAQElHRkQ+Pj48Q23Ov7uzoZqbnZyeo7Y9LjQvLC4+38nuXnBNNismJyorLThU18zM1l5CODM0OTgvKSo6yb+7uMq7n5ycnKGfnbgyMDQ1NzXkq6y7x+FeNiAfKysuRFnFssZf8005MCwwSGbp31pIPjlKyrrGULefm5iZn6GjxzMqKj1yTdCppa5eMDs4HholRcrTbbmt0TEsMjYrKD3My+1IPDctJi/bsrK9xaOcoqCbpr283jssKuyww8atqr4vICkzIx4wu63EStzGOyMiNE48Oti3yT4tMDgtKT2yp6qwxLein56Yoc3tTD00LFCwub6zu3MxISUzLDPvxsDjPTw6LiwxRWtPS93WUzsyMTI0QOC/urCus7e6s6iek5O4J0k9Ki46vKK5WM7pOCYdKmdgWsrG3jcoLj43NEHRxUg4T9xWOTE8VUdKzbawt727sK+yq52QkG0eNTYxMzq0oMY4PUE3KB82vsTT0vJbLSMtR0hERtrDRS4+6tBuRk72Sjpfu7Cyu7mxsbW0qZiNmyUgLzU6OPCjqjwsMjYwJi3IrsNXR0Y5KSg+2P1FPmNmNy9LzMfbYlVYTkBVua6zvcC5r7G4rJiMnCMfKjJIOe+iqzInLTc6KyzJr8ZKPT9ELSpC12Q9OE5rOTNMy8LN8FJMQz5Uva+wur66tbO0r5+OkzceIzFn7k6urUEnJi1APzJIv8VsQTc/PjA1UWdMPDlFRzw+XM7Kz2pGRUlK1rewt8XOwLOwr7CbjJ0mHChRwU7qq7ktICQ65EMyWMHYRjc9Xj4uOVRhSTY6TkI1Pe3Fx85fS0RDXsOztL/OyLm/rq+ulo2oISAnY7f0z6vNKSInO9dEON3J61U8RFY4LzxQWUo+PUM8NDZewb7OV0tNSGS/srTA1+C+s7KurZOOtCMgKt+468WyUSooKj/ZQTzY03NVP0ddOTFCVlE/MjlJOTA93L6970lSVVJvv7Czx2Xeu7GytKKOkkchJjm/xGO1sz4nJS105TtIztDVTj1UVTs9TVZLNi87OjE1Ptu7xFxPSUlU4revudpnzbiyuLiYjJ4wIChXu97ArcowJSc4b0g+2b/JVzo/VUI8SF5TOC0wOTUzOlPNwd9NVmlSaMm4tMJu5bqzs7ugjZTXJiQ6wFRgra9SLCIsTD41TsW3wUI9Rzs3PlnfRy8uMC8vNUfRvs5OSU9Sd8S2tsLW0b64rbaijpO9KyQuzuPtsLB2LyQoODw3Tsi3vFU6Pjw1OUxySjMuLzIxNUTyy81RSVVd3760tsDf28W5q7CkkJG2KSIy5E1QtK7JNCUrPDgzRMeyulc/RT01MTpTSjQuMTU0NkJfycxXRklr1ce8t77hyMi0q7GkkJOwMSQwW0BOt7C/SCwvOTI1Stu3ueb6Wzs2NDhBOzIyMzg7MzlZ9WhLQ0lL9MjDxb/C6r2ssKykmZSeRi0uMklHSbiuvGk0NUc2LDrgvLrL3dVQMy0tLzQyNUNOREpFO0BdQD5uXMvBzcPOxK2strizp5uerLt8SUUwLUVr4MvOyMF9REA+QUdKTVhta1pTUUc+Pj5GRUBGTExTVlx6ZFFc8NfN0t/a1NjT3N3OzszIxcTDw8LCxcnS3/15bmdfWFxaVlJMSUZERUlLTE1PYG1cWVhaXmJWUk9MT1JZXl9ic/P76NnMx8rIxsbH0N3sffrl3dTQ09LV3+zo+m938Gr872rmeFFPUE9VTUhPYG5nT01RTFZOSEVLSUtLUdfPzsW9ubS3uri5wMXW08jJyszExdlqZPhTRUxs29DeWE5IP0BBPkBAPT49OTUwLC87PztAPs2foqyttK+tVDdUXdrN6rOkpKav22hLNzY3Pda0rrPHTToyLCoxTsaqrLqyzS4eGiAxLCUsOsXHLzPM0s39NG63y8i9ua+5cN++uLW0sLK9YT5Yyl5Ov62bnclDQz0qHh44urfRb9PB4zMqNj5ATsmtrtM/PVHd2tpfRN6wrcZATb676k5UTj41LTNFZ9/byb2+xdlXT09NY8Kyr7hvNi0oJCt4sain3EhTLjAuLMmtsKevtb3POjg/4LvFrKjLXrq63UdMzr7zZ8nDYy0jLUBNNSo1177QeFPZzz82OkRWbFW/uOE4MD/MtbXP1L7Cwk5Az7i30lq8ra/KRDxXTDYzVLmts73MSDYyQ8m1rq2utMVKPlrc3Odt2M1FKSErQj0zQ7O15S0mPNVRNDDrq8wqLDe+pcBOys3SyFBEva64yHTf1l49QnTCt8DMvb/dVkjdu73Q5U0+P0Jqu8LP/XJR/PlESkbU0sTAwL64yulRNzMwMD1uzbi72D0rKTFD1r61sb5SOzc7QWS+sq2ww/1pST8+aL+wsstHP0Frzci/uL7cXkfaz91t6tLO8WxkTW7YSjg/R+nN3dbB20tDOTtM3LrESDY7+MZbMCo3y7vvOC85WlI+O0nPwntDUcOwsLGtq7LLYmne0GxU4sa/yvtdbmNMSXy8s7vgS01sXkI6a7Ort0cuIdPOO031vaa0NDo6ZNBcPtC8u8hCPdO3ucZIROfIyeFbW2RdQzs8QFV0fH1cVko+PD9MTFBOWuf2T05e5sC+v8PHzvZBPk7mw8HV4PphYlxV7sTKzNXmwL3N7E9FYs/Ky+5LR2llS0E+dcPByMtnXk86ODtKyLu2tLrIUzUzP33Bvsje8GRSRURc1cvN2fTp6l1SSEVf825WTVBm6WpSSVJz6uz77drT1+5qZOXHw8fP29jT3+5eYOjNw8XPfXxsYV5dWurR0dteTlNTTUxNWe3b3HRp5dvvSDxAta2ktzEzLjI9fDNDSNmxsts6My06PDg6eLemoa3cPztQxsna0L2vrLbtPjc8XNbOz9fSyMfTYUM8PkdPUEc/QVB93vZVTlZs7m5PSU1e4tba4eXq7HddVldi79jQz9HX3/NrXlxhcerb1M/P0trk8vnz7enm4t3b2tzi7v52dHl7e3t+/v19cmxpaW11e37+/vn09v10a2lpamxtcHR3dXFvb3N2eHZzcHB0dndybWlpaWlnZGJkaW5zc3Fxc3Z5eXl9/Pf09fr+fHp7e3t6e3t8eXNubGxucnh8fv37+fn5+vv7+vf08fDv7/Dw8fHz8/Du6+jn5ubm5eTj4uLh4eHi5OXn6err7e7x9ft9eHRycHBvb29vb3BwcXFzdXd5eXh4eHh4d3VzcnFyc3Nzc3Nzc3NycXFzdHZ4eHh4d3d2dnZ3eHl6e3x7e3t7enp6enp7e3x9fv/+/Pv5+Pb08fDu7u3t7u7w8vT3+fv9fnt4dXNxcHBwcXN0d3h6e33//fv49vTy8O/u7u7u7+/w8PHy8/T19vj5+vv7+vn49/b19fX19fX29vb3+Pr8/317enl3d3Z2dXV1dXZ2dnZ2dXR0c3JycXBwb25ubm5vb3BydHZ3eXp8fv38+vn4+Pj5+vv8/f7/fn19fHx8fHx9fv/+/v39/Pv6+vn5+fn5+vr6+vr6+vr6+/v7+/v7+vr5+fn5+fn49/b29fb29/j4+fn5+fr6+/3+fn59fX19fX18e3p6eXl5enp5enl4d3Z3d3h4eXl5eHh4eHh5eXp6enl5eHd3d3d3dnZ1dXR0dHN0dHV1dnZ2d3h5ent7fX5+/v39/Pz7+/r6+vn6+vr6+vv7+/v7+/v7/Pz9/f3+/v7+/v9+fn5+fn59fX19fHx8fXx8fHx9fHx8fHx8fX19fX18fXx8fHx8fH18fXx8fHx8fHx8fHx8fHx8fHx8fHx7e3t7e3p6enl5eXl5eXl5eXl6ent7fHx9fv/+/v38/Pv7+vr6+fn5+fn4+fn4+fj5+fn5+fr5+vr6+vr6+vr6+/v7+/v8/Pz8/f3+/359fXx8e3t7e3p6enp6enp7fHx8e3x9fn1+fH1+fn18e3t9fXx7ent7e3p8e3x8fHt8fH19fHx8fX18fHx9fX18fHt8fX7//35+fn7//v79/fz9/f39/Pv6+fr9/fz6+Pj5+fn4+fr4+PT09PX29fT09PLx8vT4+Pr5+Pj7/P3+/n5+fX18eXd4enp7fX3+/Pv9/Pv8/f79+vr7/P38+/nz7vH29fLt7/r383t3b3zz9+3u+vXo4dvk+eHe2+vd09bcvc1a/eNzblBEXUZDPE5oq6hTTnPYVD46PztOXk5MTFfr12pPTlpd/XRsXmFuZUxGR01bZllVXFlbYltZWFdYWlJWXnHs7nh8c+7e5+PZ0M3O087Ly8jHycfEwsDBwL69v8DDxsfHxcLFyMjJyszNzs7S09bV1djg6ePc4/Pu8vpzaWZrXFpcYWVfWlhaWFlUUVFYWFpXU1JUV1pYVVVWVVVWVVdXVlNTUFJTUlVYVlZXWVlaWlRRUVdXWlpYV1dUWFtbWFZWWlhWV1dbYF9YVlFUWVtZWFdZXF1cW1ldampjX1xidHJnamht/XJtdH758mtodHVxdW595+vs6vHw5unq7fn37ezm6u/k3d/i5+fi5uTb2dnY29XP0dLT19TT09TV2NPQ0dLX19XY3Nzd29rb3t/d3d7d2tvf5+HZ297o7+nh5OTm6+Df4eLo7uji4eb7cHL77uvq6+/z9Xl6efzt6v1zcP7t9n1+/H11aWlsb3h6a2drbHFva2xtaGZkYWRpamlnZGhub2xiXF1la21kX2hubWtnam5oZWZiYmRkbHNtbGZlcv39+3Vsbm1vc2pma3BwaV9kbm1nX11lcm9tbnX28ntvbnV+fnVubXF5/v1+cmZlaGhoamtxdm9ua2twff52Z2Vvfv5yaWz793pram59/ft+dnX9+nxya3B8/n54c/z39/j49PL2+Px7fvnw7/Tz7Ojo7ff37uft/Xrw5eXx/vXq5e79e/Pp6O/4+vrx8fN7bmppaWdnaGtxcGhgXV1hY2ZnYmBeXmRpbW9qZWRlaWxpaG16/XNnZ3P39nlvc339eXN89fH4ff7z7/P18evp7fP07uno6+vr7fH49e3n5efr7evo5OPm6+7s5+ju9fDq6O38fvXu8P13/O7r7vn98+3u8fn27+3v9/779PL2fXV0d33/eXNydn19eHd7+/f9eXd8+vn/d3Jzd3lybWtsb3Bvb3N3d3V0dn358u/v8fPy8/b49O/v9npxcXR1cG1sbW90dnRxcnr8+316ffnz9Pp+//r29vt9enl5eXZ0dXl+/Pv9fHh2eHz//X15dnd6enh0dHd6e3dzcnd8//97eHZ2eHl4d3Z1c3BwcHN2eHl3dXV5//n3+fv8+/z8/Pv7+vj3+v1+fv39fnt7//59eXr/+/v/fH77+Pb29/f28/T3/P359fX7fv328fL3+fbw7vDz9fTx8PP3+/v4+Pt9e379+356en78/Xx4eXx+fHh2dXZ2dHFwcHJ0c3JxdHh7fXx9/vr5+fv7+ff2+Pv7+/v7/P39/Pv7/P7+/fz8/f////7+/v36+Pj5+vr5+Pj5+vz9fnx5eHl7fHx5d3h6fvz49fPz8/Tz8e/u7vDy8/Lx8O/v7/Dz9vj49/X2+v5+fn5+fHx7e3t6eXp8fv9+fXt6eXl4eHh3dXRzdHd5eXl4eHl7e3t7enp5eHd2dnh5eXh3d3l6ff///n59fH3//v3/fXx9//38/P7/fX19fHt5d3V1dXZ2dnZ3eHp8ff///v79/Pv6+/v7+vn4+Pj39/f39/j5+/3+/359fHt7e3t8fX19fX19fHx7e3t7e3t7eXl6enx9fXx6enp7fHx8enl4eHl6e3p5eHd3eHh4eHZ2dnZ4eXt8fv/+/fv6+fj49/f29fX09PT19vf4+fr8/f5+fXx8e3p6eXp6e3t8fHx9fv/+/Pz7+/v7+/v8+/z8/f39/f38/Pz8/fz9/Pv7+/v7+/v7+/r6+/v7/Pz8/Pz8/Pz9/f39/v3+/f7+/f39/f7+/f7+/35+fX18fHx7e3t7e3t7ent6e3t8fHx8fHx8fH19fX19fX5+fn5+/37//////37//////v7///7+/v///v/+//7+/v7+/v7//v///v/+//7+/v7+/v7+/v///v////////9+fn5+fn5+fn5+fn5+fn5+/35+fn5+fn5+fn5+fn5+fn5+fn5+fv////7//////////////37//37///9+fv///v////7///7+/v7+///+///////+//7+//7////+/////////////////////35+fn5+fv//fn5+fv9+fn5+fn5+fn5+fn5+fn1+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+/////////////////////v7+/v///v7+/v/+/v/+/v7+/v7+/v7//v7//////35+////fv//fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+//9+/37/////////////////////////////fv////9+fn7//35+fv9+fv///35+/35+fn5+/////////////35+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fv9+fv9+/37///////////////7+/////////v7+///+/v////7+/v7//v7//////////37///9+/37/fn7/fn5+/35+fn7//35+fn5+fn5+fn5+fn7/fn7/fn5+fn7/fn5+/37/////////fn7/fn5+fn7/fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn7/fv///////////v///v/+//////////7//////v7+//7//v7////+/////////35+fn5+fn5+fn5+fn5+//9+/35+fn7/fv///////37//////////v7+/v3+/v7+/f39/v7+/v7+fn5+fn19fX5+fn5+fn5+fv9+/35+fn59fX19fX19fH19fXx8fH19fX19fX59fv/+/v99ff/7+/77+P7++w==", "UklGRnROAABXQVZFZm10IBIAAAAHAAEAQB8AAEAfAAABAAgAAABmYWN0BAAAACBOAABMSVNUGgAAAElORk9JU0ZUDQAAAExhdmY2MS43LjEwMAAAZGF0YSBOAAD+/v79/v7+//7///9+fn5+fn5+fn19fX19fX19fH18fHx8fHx9fX19fX19fX19fn59fX5+fv9+fn5+fn5+fn5+/35+/37+/v79/fz9/P39/P38/Pz9/Pz8/P39/f39/f79/f79/v7+///+/v7+/v79/f39/P3+/f79/v7+/v7+/35+fXx9fXx8enl4eHh3dnVzdHV2c3JwcG9vbmxra2pqamlqamtra2tpaWpra2xrbHN0dXd3dn17fvj07+/59vD06vDy5uLf3dnX1NLOz9DV19bb2vrrbV9OW1TRucvL1c3S1PxsSkpQTGduZu3bycjIxM7Bwbq9u7++wMTH6d1mfF99eGdsX31hYUtOQkI/Pj06Ojo6OT46PDc6Ojs5ODg2ODk8PT49QENGSElTUV1YYFlgZ3V23vLn3dvT0M3V5NvOyMbJzc3OyszLz8/OysrNx8nKzMrGw8bJzc3Q1c7LxsnL0tre1s7W0t/q2tnZ3+/o9fTt3dPW1+Xp4dbd9u5ja3jt3d/n3d7e29vg7O/9ffbj33Rq8OH75NXd5+Ls7H3zaF997/dpXVpn9nddWF1lbPteVk9WY2FeZ2hodGVq/O71alZYW1taXV5VUVRn/vrub1FPVHzv/lpVU05c/e/+VlBVZe1wYltfanfl9mVfaPv8eG9hZHF0bnV1b/rq5ujh8urf5Ojr/nd82tDa8O7c0c7fbnJ58erle2Bs9uXb629w7uHj8nVqaHfd1/JeXGPu4vNpX2D12NDX73D23tTY6GVea+fY32pcYfDh5u5oXF9ja11TVVde7t7d4+fc0MvM0trd1M3N09rVzcnIzdbc3trja1pcZ15MQD09QUI5MzM5Qkc/QFPOuLGvrauop6mrq6usrrO3uLq+x9DdcUw8MiwmIR4cGRUWHCIrMDrar6inqquprLjF7VtdYdq9tK2ppaGjqqyut81OPjkzLSkkIyIfHBodKiwpL0TFsLSyq6iqrri6vNxUYt3Hv7uyq6qrra6utb/GzGxJRT83LSclIx4bHSkrJjF0w7ayrKOkrK2tsr/2W9nY/865sa+vrKqssLS4vdZXT0Y5LiopJSAdHSItJihc0d21q6ilq6ulrr/A0GFVTe3N4L+wtLKurq60u7m/blRMOi4rKSMgHh4lKiErWkBFu7Kxr7CnqbevrcLCu8vFwMm+u7+7uLu+vsjP7FRGOi8sKCMhIR0gLSsnY8tet6itqqepq660ssLvxs5O5dJm9+B5dWhd6etS9M9kYtNmSk8/NTIvMjUsM09AQ8S+yrm0vsHD0l1JQVJKPmrK38GwtLCrrbKwsr/R1Fw+PDcuLSslKCwkJi4sKjl4TGC0sr6rpa+tpq62rrPFwL7W3M/d4/FdXEs9OzMsKyYnLSQmPDow0L3ZsqmyrKatr6+4u7/h191MS088OzsyMzEuRD8y3sZKuqzGtKm/vrHYYMdKPnBDOnVOQGFQPkQ8NTcsNUowPL9d36mvtqKnsKWnubG119nkRkRDODQxLi4pJywkJD4xLM7GTq6msqafqqmhrLWvvd/TYUVKRDo5NzMvKywpIS04JzTJSWmprLKgoKujoa+0r8dq4Ug8PzkxMjAtKioqIiY7LircyEywo66lnaWln6mwr8DU10NATjcwOjErLConJCIxLydExErCpKuqnqCloKWtrrzPz0Y6SDouNDMsLCspKCEtPCkzvfvxqKasoJ+ko6Wsrr7a0kw1Pz0uLzQuKyssKSMqPC4t3Mj0s6anpKGgn6WqqbTQzPc8PT41MjEwLywqKyYkMTUtO9vRwK+ppKSloqGpr6+722FKPjw2MzQxLzAuLCwoKzUyMkf00L2zqqWnpqKkq7C1wG5JQDg1MjAyMi8vLzAuKi85NTZJZNm/ua6oqaumpquwt7u+8URJRTcyNjg0MDE0MS4wOTo6Qm7WzsCzrq+vrayutLW4vs7X7mdORkJEPTg4Ozk0MTU4Nzc6RExXbdXCvb25s7K3uLa4wMTJy8/p9OV4X1ZQTEtFP0A+Pjs/QUNFUVpk9trTy8fCv769vr7DwsDJys/O2ebg5mtsb2xRWFdPT01PVlNaVWN+92jv3+Pj3tza1OXg1djf8+/f5vV07+PmYHPn5mxv8e3p7mzu9O16eHzs8mb85nPrXf5sZfhkY19mXFxiZnZoaXR2an3+9HRubGv4+/14a/d493nseeVk8eXte+x9cGxt62pudXpneer2bOxtenzj7exr6959fePqfW7w+PXg63todvFx+W1kfnRqaf1dfvRlaubgePF57d1qbNfhZmTm3tz9dvPicWXw8OhcbnzfYe5kc/xjbX5u5/Zw+OXhfmNuX2pfdP3v7/9r4eBw93VX8Plu6ORzY2/TYOjq4O1aW+l+bHnt6vVuWmLg/mrd1t9s7tPW3tz3W+Zwfuly4eXwbnzPaGxj/FxNYeP4YWlW0GxHWO5rTUl32HdgTXnNfExR3tVQVu9jaVpm7NjSY05j105HVdHTY97M1tfvWk9fZlZOXGTefG5e2tfWXFFizfpRSebd2OtU/sPP4NDd5MzqX2nQydhn1dPMzM7a5ufd3GlPZs/Ta/ze1eZreeri+GDv8lFeaeHQ3Pr4TlfnZGxURn7V6mNEUOJyTUpT2dXsX1v71vt8UExWa33sWmXl09bebn7r2uPu8dTe+2l+aeDKzujle+fl62Bba97Q4lRMZNHLdExKWeHP92BXcNDYa19eddnneWtu58/V2PxZTFZZWl5f5tfX5Vo/TFnZ0fBHSkhT4NvaaUpOXF39VXDnzNTvYFZg69DS3WRgYu92a1Nmd2f1aF9m8FlnU09aV2Li28bF287Oxr3D0MzQwbu/xN5g6Ovf6UhBPz9CPjg2OD9OU0w8PERbzsC9trCuqqqtr7e9ur68w9dlS0NFRDwzLCkqLTA0MSwrKi06Ur6vqqelp6eprKyurq6yub/ZXkxKTk1ANy8rKyssKyolJSgqPM6roJyfprK+vbeur7S8x8e/w9NPOzg6P0A4LyopLCwrJR8hJze/p56co66+1si6tLO5xsvHx8fmQzs6P09LPC8qKi4wLyYgICc5vqCal5ypwWFmybu2vc7a18DAzFg7NTpBTkw5NC4wODIsIyElMHekmZWWobVPPE3RvLjI0+vOv77QTjk0OjxJQDk2Mjc2LikjIik0xZ+Ykpekvjw0P3vCvs/b/drGyNhZPTg5OEJAQD88PDgwKSciKy/Nn5iRlqG9Oi44TMS8wMrZ3svGyuxNOzg3O0RGTEpCOjIoKCIpLUennZOUnKtpLzA2c8PExdrh1cPAw9xNOTQwNj9HXE9ANysnJiMrLcelmpOYn7RDMC847dDBz+ny2sO+vsxROS8sMjlY6/5ONiomIiIqLr2nmpWZn7VLMi83X/PG1+nV0b27vMRkOzErMDhO2vFcOiwnJCIqLM+pnZWana9uNzI2UnXP02nV0723t7zVQTIrLDZD19bq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c9LvNCYmMVnOQCogHS8ub6ugl6Gwazrnr6Slul5Tv6epuD4qMEu7uOY5LjRmy/05JyYvS9tKKyEeJzsyvaibl6i9Pj27p6Os7UnYrKGuWyoqSL2z1zYtNGS/6TkmJDBM0ksqHyAmQTU+rKKVnbZeNMyooqrXQN2toKpjLShMu7TVMis2W7zMNykkME5kQyshISczOy63oJaXs1U24aifq89BdKuiqGYrKkm6t/swLDrlwd4yKCg0Uk87LCMjKC05LduemJWsTT/Np5+u/kP7qKKo+ywtULu5UC4vPs3FXDIoKzpLRzUrJSMpKzUuTp+alqhNU8enoK5ySvqqoqvUMTJhw8VHLzJH189IMCstPUI+NSspJCkpLzFBn5qYo17cu6mjsmdVw6uirvU3MlvLzkQxM0bj7UUuKy05RDw2LSonJiovNkihmpqf08W3q6W3YUvAqaOr6z08Ts1yPDMySt/fTC8sLzZAPjUuKiUlJSswN6ianJ6yuKuop7FlStG0qazNZ2vvvtBFOS84TElDNS84O0BTSj41Li4uNDMzYr+5tLayrKqnqK2zt7WztsLl8+fb31xKQDw/Pzw3MjM1NTo9OzcxMDAyMjA3S2LfxbmvqqWioaOmqaqts8XtX1dXTT86Njc5ODc0MDA0OTw6NTEwMjQ1MztZ2ce6s6umoqCjp6qtrrG831ROU1lLPTg2ODs6OTMvMjc7PTkzMC8yNDU1PXvGu7OuqaOhoaWqrbGzt8ZkRkJITUc8NjM0Njc2Mi8xNzw/PTg0MjQ4Oj1J2MG6saynoqChpaqusrW5yGtIQkVIRT85NTM0Nzg1MjE0ODw9OjQxMjY8RFzJu7ewrKeioKGkqq+3vL7LdEs/PDw8Pjw4MzEyNTc3Njg6PD4+OjY0NztCX8W2s7CtqaSgoKKor7nEydF9UkU9Ojg7PTs5NTIzMzQ1Nzs9PT06NzY4PUZlwLCtrKuppqOhoaatucbY7WNQTEQ+PDs8Pj48OTY0MTAyNDg6ODYzMTM4Pkrdt62sq6uppqSho6mxv8/oaVpVU0pEQT8+Pj49OTQxLy8vMTY5ODY0NTk+TuK8rqurq6upp6Wkp624yd1wXlRNT0tGRUA/Pjw7NjIvLi4vMDM1NDMzNzxIbcu3rKmpqqqqqKamqa+7zedwZFZRVlJQT0tIQT05My8tLCssLjI0NTU2OkBV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Ix0cIickP666y7Sz1DQlJy0lJDfdvL7SyL3PVUNBWc+8raKjqKmorLO2r62xt7SywV5BPTIkHB0iJSE5ucDCrq7LRSsoKyUgKz1q0NzStbDKTEt4ybu1qaOmqaamqKyurq2zvb3IaT83LykfHR8nJCvyvb62sMLeQCsnKSUkKjdO39HFsa253+O9rq20sammqKmpqKits7a5wtlRPjgwKiQhICMnKjJL1si+vcTUZkE3MzAvMjlCWdrGvLe1t7i2sbCytLSzsrGzt7m8v8PJ0+D/XlNMRT87ODY0MzIyNDU2OTw+Q0dLT1VaXmVufu/l3trX1NLQz8/OzMvJx8bEwsHAwMHDxcfJzM/W3el7ZFpRTEhEQD8+PTw8PDw9Pj9CRUhMUFhfbPzq4NvY1dPR0NDPz8/Q0NDR09TW2Nrb3d/h5Ofp7O7y+P55cWxpZmNhX19eXl5fX2BhYmRmaGpsbnF1eHt9//79/Pz8+/v6+fn5+fn6+/z+/318e3p5eHd1dHNycXBwcHBwcHFydHZ4enz+/Pr39fPx8O/u7u7t7e3s7Ozr6+vr6+zs7Ozt7e3u7u/w8fP19vj6+/3+fn18e3p6eXl5eXl5eXp7e3x8fX1+fn5+fn59fn19fXx9fHx7e3t6enp6eXl5eXh4eHh4eHh3eHd3eHd4eHl5eXp6ent6ent7fHx8fHx8fHx8fH19fn7//v78+/v5+Pf29fX08/Py8vHy8fHy8vLz8/T09fX39/j4+fr6+/v8/f39/f7/fn59fXx8e3t7enp5eXl5enp6ent8fHx+fv/+/f38/Pv7+vv7+/v7+/v7/P39/f7+/v7//n7/fX59fX18fX1+fn19fX1+fn7/fv7//f79/f39/fz8/Pv8/Pv7+/v7+vr6+vr6+fn4+fn5+vj5+vn59/j4+fn6+fn6+vr6/P3+/f79/n5+fX1+fX1+fX17e3x9fv3+/v37+fj39/f29vf19Pb19fj29PPw8fT19Pb3+Pb4/n15/PHw7/P08O7s6e7y7ezr7O70+u3o9ntweXp6eP32al5kbWpoaV5nbFtcY/jfcW5aTV5PNFn4RENn2/TRw9Xuyry68D4yMjtER0pOUVJb7N16VUxJQ0RDQ0lLSk5UW15bWl/p2OFw9t3YzMTCwb+9uri1sbCwsrS2trSysrO0s7S2tre4ubu8vLq7vsPIyMXIy8vM0t34c3VZVVljbmBUTUpIR0ZFRktMSURDQ0RFSEVBQUI/Pj09Pj8/PTs5OTo8PkA9PT0/Q0RAPTk6P0FISEZAQD9HTE1IRkVKSkpLS1ZdZ15bUlFPVV1odvft+3hmde3Z193j6t3V0Nzb4eLb2tPOz9PX9Ojud2BVUmPZ0srP3ntdXmRncPPj3elpWlli7ODU0+F4YWjo18vMyMnIxcXHztXQzcXCxMjR0c/JytHk8P3l29jM2tLd5tng1NPRz8jSyd3b3OLQ1svW0unp6N7QzcrJxs/Vbl5r68rCv8jSXmVn0sW/xdb6Ze/hztTQ5ux76uvrfmRdWWdqcGttffDmd2dPSURKUmbo7/5dYlNZSUhHSVFRWV1bV15d9HRgVUtMTVtq3NvY5l5YTVNeZmFdVWzl1tpfT0lSbeXe53BqXl1gWltWWFdhYWhZXFpo8/P1YFZMS1Br39DX5ltMTU9t6+n0XFdUUlxZWFdOU1tifGJbU1dbfvH5cFlVWVxsdmRrWWBldO7q6ebf39nj5nVvbezf1NHU1ufr/Prw6ff5ZGRm/d7a2uPxe3pwdGNlXmJqfurs92VaVlpw4NPNz87S1dz2bF1dau/Z1Nnnb2hlamFZTUdERUlMTEpGQT8+QUdTd9zPzc/Oyb+3sq6vsri8vr6/wcviVkVAP0NBPDMpIR0cHiQ0cLiqp6ersLW3s7Cvsbm+v7itqKaqs8La283KynNDNiwsKyolHhsbHy/Xq6Kgp7PMTkdLaMzFvr69t7OtrbC50GdOV+XPx9VmRDgyLiojHhscIjbFqqOjqrbOcFpl/fLd1cW6sayrrbK9y+Di3dfY521UTEQ8Ny0mHxwdIjTXrqamqrPC0Pj6ZFpdYdnBtq6srrXAzNzV0NPVfWNaU1VDOi4mIBwcHilHvqunqa28xudlW0pMSFPexbOuq6ywtsLL1d7c+3VcVlpNSTkvKCEeHSArS7ysqKmuuMXS8GVVTEtP/8e4rquqrbO8xtDW29/3YVZRTko/Ni0mIB4fJjXctquqrbK8xM/b71xUTVXuyLevq6qtsLnAy9fd+WhZT05IQjoxKyUhHyIsP8myrKyvtr3Fy9jnXlBMTW/Nuq+sqqywuMHJ09jmbFpMSUI9Ny4qJCAgIyw/zbStrK+1vMbL2e1eTkpKWti/sq2rq66zvMXP4PxcUUpFQTw2LyomIiEkKzvwurCusLW7wMfN4GxOSUdO8Me3rqyrrbG4vsfQ5GtRSUI+OzYwKyckISQpN2a9sa6vtLm+w8jS7VhKR0try7qvrKutsLa8wsrV8ltMRT89ODItKCMgICQtRMy0r66zub7Dx9DsVUZBQ1TVvLCsqquusbi8w8zdYk9GQD88ODErJiEfICY0X7uvra61ub7Cx9tiSD9BTNy9sauqqq2wtbq+xtLwVkpHREU/OTApJB8eICc63bOsq621vMLM2ldFPDpCZ8Gxq6ioqq2yuL3FzeRrVE5QT09FOS8nIh4dHyc+ya2oqa23v8veYkY7NztNzLOqpqaqrrW7wMfN1+Dp7e79aU5BNCwnIR8cHiU7vqqipq7BalJJQjs1OEPZs6mjpKmwvMPIzMzY1su/uLi/4EM0LSgnIyIeHiY6uKWgpbhlOzk/Pz86Pum4qKKkqrrP6+DKxcTEwbeuq66/TTQrKysqKiQjHyAw+6qjprRUODI6Rj9BP/qzp6ClstVKVta+ub7AvrKqqa3AUzcyNDEuKCcnKCciLEK3paewUjAuNktPSE/fr6WiqsZMQV++ubq/v7Grp6y85UpJRj41KykpLC8nISA4vKShslItKztQ9VBJzq+ioa7SO0DtvLK9xsC0qKiuwWRl8OFdOi4pKi0uLCEdImKrn6PJOSsvS1BLQFC0pZ+nwkY3SMm9ucHAs62pr8XrXdrL5kw2Ly0sLCkpIB4hT6ugoMk1KytEWExVYbCin6fLPzhLxr2+wbqtqKm0z2Fuz8baVDs0LywqKCglHyEusKKerTstKDJPTVjXt6OgpbtGOT/pvsLBva+nqbLUUWjJv8buSD04LiomJickHyI2qp+fsjMpJzJRU3zAr6GgqL5DOkR2xse/t66nqrfVXebLyNFpV0c6LickJSYjHiAzr5+hrDwqKSxLZXbFs6egp7NhPkNg08XNuq+opa6+2e3P0e9dVFdJNyskIyUjHx4p3qeipb02LS01ZFJ5wa+loam24U5jXvDr376uqqeuusLP225LRUZDPC8pJSMkIB8jMM2rpaa2Rjc3QedgVN2+q6SmrbrOy9XjblbfvK+rq7K2vMfjTT04NS8rKCcnKCgmKC4/1bm0uMXW0snCws3Y0ce7tra4ury8vsLJ0NjX09HP09nd4ex7YFNNSkdEQj8+PTw7OTg3ODk8PkFGS1FffeLZ0s3KxsPBwMDAwcPFx8rMzc/Q0tTW2t3j7f1vZl9cWFZTUE5NS0lHRkVFRkdJTE9TWV9qfe3j3dnW1NLR0dHS09TV19jb3d/i5unt8fb6/nx3cm9samlnZmVlZWVlZWVmaGpscHd++PHt6ujm5OPh4N/e3t3d3d3e3t/g4uPl5ufo6evs7e/w8/X4+/3/fnx7enl5eHd3d3d3eHl6e33//vz7+vn4+Pj3+Pj5+vv8/f5+fXt6eXh4d3d3d3d4eXl6e3x9//38+/r49/b19fTz8/Ly8vHx8fLy8vPz9PT19fX19vb29vf29vb29fX09PPy8vLy8vLy8fHx8fHx8vLy8/Lz8/Pz8/T09fX29vf39/j5+vr7/Pz9/v//fn59fX19fX5+fn7+/v38/Pv7+vr5+Pf39/f29vb29vf39/j4+fr6+/z8/f7/fn19fHx7e3t6e3t7e3t7fHx9fX1+fv/+/f38/Pv6+vn5+fn5+Pj4+Pn5+fn6+vr7+/v8/P39/v39/f7+/v7///////////////7+/v7+/v7+/v7+/v/+/37/fv///35+fn18fHx8fHx8fHt7enp5eXl6enp6eXl5eXl5eXp6e3t7enp6ent7fHx9fXx8e3t7ent7e3p5d3d2dXZ1dXRzcnFwcG9vb25ubm9vbm5vcHBxc3Rzc3N0dHV2dnd3d3h5ent6enl5eHd2d3Z0dXZ4ent9fX7+/v7+fv5+fn7+/X59fnxzcG1ucG9ybHhnYWf2fMTJWdzz1V5ieedbeWXr19/n2NDU2NjP1tPU0NLR1dbc5ez17+77//x2bmxiZV9bWllWV1VVVVNUU1JUVFJTU1RTU1RVVVZVVlhZWVpbXF5fYGNlZ2dpbG1wdHN3fP9+/Pf39vb29PLw8fDu7Orq6eno5+bl5eXm5ubl5eXl5uXj4+Lh4eHh4eDg4uLh4uLj4+Pl5+fn5+jr6+rq7O3v7/Dy8vPz9/j49/j8/v7+/f5+fXx8ff3+/v38+fn5+fj39/b09PTz8/Dw7/Dx8O/u7e7v7u7v8PDw8O/v8PDz8fDw8vLw8vPz9fT19fT3+f38+Pv9/Pr8fHt9fXx9+/r9/fr5//33+f96ffv6/X14d318+vr79vt9cm53enFtc350b3Nzb21zdXh1fP569/t7d3X9+Hloa21jaWZrdWZfYGtoZmhnbWlfYmNgZWtyc29ral5fbGpgWmxpYnZ87XRr08zbe2lqV15bU2lsYGDlytDm5uxoXGLs0tHg6vno63nw6efW1eTb3ndgWVdXUVJbXV1bbtzb3eft6GlcZV9fXFlv49/SysPAyc/L1f1VTFNYX2pZWlFBOzg2NzY2Oz9CR0lKS0g/PD1CTFz2zr2yq6ahn5+kp6OhsFEwLC8uKzjnvbzYTzsqIR8hLDxZyLi2vNk/NTMrKzM8Z9nhwsLMx86+rqyoo6KgoaeopqOsPigtO1Vk57Kru2I9MiwjHyc5X9TQwLrPQzYxMi8uOk9XWFpUSj85P2nOuqukoqOnqq61sq+qqP8tMj1NSD7Jr79NNC8vJSAoOfDP5c3JUTYtLjIwNEvi9VNFRUE2NT5Zw7avpqKmqautsLewqaapfiotPD5KVsKzyUQ4LywnJC5H6M3dcFc3LS8yOz1AWvtaRzs/SD49S+3At7KrqKmsrq+ysKypo6OwRCUsRkdO072zzTIvLyspKjTfzfF4XD8xKi48QD5Hat1ZOjlLVkZETdK+vbmvq6qtr66ura2spaGn1iUiPEFC28eyuUI0NjAtKjBtyNDaXD8zKSk0PkZOWupmPTQ+V1pabHvW19a/r6upqqysrrCvraiipMEpJDM+SV7GsLdIMzQ2MC017L/LY0s/MykoMkFDQ0dQXjwxO09XaXLe0mFoybuuqqyrrK6trK+sqKKi4iUnLzlHUMCsvEQ5ODQtKTXaw8reZ083KCcuNjg3PV18Pzg/S1BNXs/Pd+LHt6yqqqqrrK2vraysoaPbLiorMDtHwLLEUURAOC0rNU9u/tzcVTYqKS0vMDI7UEs+QUtOSkVpz9vi1cO2rq2sra6urqunra6io7w3KS4zLjnPtrz+U2JALSouPEhIXs7WQC4sLSwpKzVITktHW3pLRl3c297OvbKvq6mrrKquq6ezsqOisFgvMDEqLUjMws7ex9w8MC4yOTY8c91dQzcyLiglKi40PUhe6N7mYXF+ZnvWurKvqKiopaiqpa66qKSos11APi0nKzJGVFDOvMZjQj48MCwwO0RFSFVVRjg0NDEtMz0/UWrOvL7AwL67u7++ubaysbOysbOys7W0t73CzNz6V0xLR0NFQ0FCQUA/P0BBQkVISkpLTExNTk9UU1RaXWBjYWp+8+3i4tzY29fV1dPRz83LycfFxMLCxMTGyMrNz9Xc63RkVk5KR0RCQD9BQkJDREZISUpNTk5RVVldX2JseP/z6t/b2tfTz87Ozc3NzMvMysvLzM7Q0dne4uXs/Htxb2leW1taVlVVVlpaWVpbXFlWV1lYV1lZV1xeXl9r/vHr4NjV09PPzc/PzcvLy8zLzNDR09zn8HHv9lxmXE9UT0xNTUxOUlhXU1JTTUpLQkVJSlRcceNtXexJXUrRtN3R6MXtvMFSv2nb1MS53HPKy93s1M7gSEhJRUQ9TlFWTF5tSk5DSkdAP0Q/PkFGVVroysK8t7CvrrCzsrzCy8TT0sbOycPBv7/HxcfkalpNRjw4PTczODc3NTQ0NTMvMTQyNTtDUd/Esqqqp6enq7C5vsxoWlls5enJvLy8urq/ze1hU0Q9PT5DPz1CRj87OTUyLSoqKikoKy4wNz5N3smwpammoqSorri6yFhb7frczL20ubmwtb/N429PPj9JQEFPTktHPz46MC0tKicoKissLjY7P079z7qnpaahoKSpsLa97lBncWnhxbq4ubWyusXQ5l9DP0lAPkhKSEM9PzkxLywqKScpLCwuNDpAS1zOwK6jp6Sfoqiss7bDW/vP7+XGu7m7u7W6ztbeW0hASEo+QEtIPzw7ODEvLiwrKSotLS4xNztBTP/Ku6imqKKipquvtrnQbtDR2s3Au7u9vLrE2Nr4WE1JTE1DRUhBPTk3NC8uLSwrKistLi80NjpESvPRvKmpqKSkpquwsrXF0sTHzMrEvb3Ewr7I3OPza1pMUVRHRUM+OzY0MjEvLi4tLCwtLzEzNTs/SHngva6tq6enqKmtra63vL2/w8jKyMXMz83W3O9mYllOS0lIQz8+Ozg3NTMyMS8vLy4wMTI0Nzk9RVD3zL20r62rqampqqutsLW5vL/Dyc3Q1uDo5OX2amBcVU1IRkM+Ojc2NDEwMC8vLi4vLzEzNTg8Qk59y7uyrauop6anp6iqrbK3u77Ey9La4/f+9PZ3aV5YU01KRkE9OTUyMC8vLi4tLS0uLzEzNjk+SF3Ywbauq6mmpaWmpqirrrO4vMDHztvta15cXl1ZU09NS0hEQT06NjIwLy4uLS0tLS4vMjU3Oj1EU+zJu7CsqqelpKWlpqirr7W5vcTN3XxgVU9PUE9OS0pKSEVCPzw4NTIwLy4uLi4uLzAzNjg6PUFLYtnEubCsqaelpKWlpqirr7S5vsbP42tYTktKSUlIRkREQkA/PTo4NTIwLy8vLy8vMDE0Nzo8P0ZQbtXDubGtqqimpqWmp6irrrK3vMLL2fFhU01LSUhHRUNBPz49Ozk3NDIwLy8vLy8wMDI0Nzo9QEdQZt7JvrawraupqKenqKmrrbC1ur7DytLffmBYUU5MSkhGQ0A+PDo4NTMyMDAvMDAxMjM1Njk7PkNKVGrfzcO8uLSwrayrq6usra6wsrW4u7/Eyc7X431hVk5KRkNBPz08Ozo5OTo6Ojs7Ozw9Pj8/QkRFRklOV2b03tXOysbCv7y6uLi4uLi3tre4ury+w8bJzdPZ4fRpWFFRT0tGREJBQ0RDREVERklIR0hLTlBQUVJUV1ldZGVpePPu6ePg3trY2NbTz9DPzc3S0dLS1tvbdm9zc3B1/fPqfXRucG1xd21lYGNiZmttbGxrbHB5+u/p6uXg4OTj5eno6Ozu8PXw7/f28vx99+/19u3xffbw/X3/dGptdHN0fHltbW1paWlkY2loY2VlYGh0cHX08/Xt7u/k5e3j3uHn5enp6Orv6uv+fvr8ePt6d3V1cHr9fP769fn8fHt2eHVrZmVkYWlpaGxua3B5dn339/Xu9f329Pj37vp7/Pl++3Z0+Pn//n3++fr4+X7++O7xfPbw8vP2+vr4+vv+eXd6/3JwfHV4bnBybW54fXh3bHv6dW/+fXhtfvZ1/W1s/v51/fLw9O3wcHNzfHv69HpyefNya259+Pr07u/y8Hp3dP74d3xz+f9udH75/2xnbvBzbGz4dX72b3D09Xjm93n6/W946u5q/OH34vHY3frb4ez2Ymf+YF9dav5s9PPw4NR2XmBWaP7ofnb0alLd52NXS9/FXklNVVtXVUhYb/bU2uzWydLneVhgYPH6X13f0dzT3dTP0+DX0M3JfHXOy3LjxdXd2FPszdxXWdPHx9NfY+Pa9lBJUWNm2ehnWuDL33BvX/ffa1hb+25hWmBfV0xddFhNVWpUUuV4REtYZFlTTllPR1h1YltITF1s+U5JU3zk/mxYUGTg4XFdYWrt4PpYWXne1PFbe9LY3+5n6dbkcV7pys/w+t/Qy+Jed+byc/Tv+O3s6N/kd23h3fldX+HrWmLb1u5eWv/d+F1iavve1+FnaefW1/Fu7N7p7ODe833l2+X76OPwefjqbV1n/H7/6t7h937o3el3c377fXz++/fy593d8GhjcPf4bWRv6d3hfWny083XfG3fz9Tsd+XV1+V+++nj8mZbV1ZUUlJXXV1cW1xfYF9fYmBaVFdo8O3y4M7GxMbEwL69vLy8vsLFx8rP2+Z+VUE6Ojs4MS4vMzQzMzMyMzg9PDYzNjs6NTdPvq6qqaajoqWprK+zuLm4t7m8xNLo8HVMOTExNTUxLzM4ODU1PEhQTUhHSEU/PDo2Ly0tMTQ4ScCqpaisqaSkq7W6t7W0s7K0t7m5vcvqYE0+NTI1NzQyNDg6NzY5P0hPVVZXXXP4X0k9ODQyMDAzQM2vqauuq6WkqrW6trOztLOztbe4vc1pT0pANzIzNjYzMTIzMjIzNjpDVvjr+PHd1+ReST46NzY0Nj/gubCxs6+rqqywtrm5tbGws7e3t7vG2mxQRD07Ojg2NTQyMC8vLy8vMztES05VZ+nY09n4W1JPTEM/RV3VyMO/u7ezsLCytri3tra3ubq8v8PIz+ZkVExHQj88ODUzMi8uLS0uMDM3O0BIVXrb1NPW2Nvqa1pXWl9r9+HWzMK7trSysa+vrq6ur7K2ubzByth7WUxGPzs4NTMwLiwsLC0uMDM2O0FNYvDf2tfY3OhuWVBOT1JYYHvdzMC6trKvrq2rq6qrrK6ws7e9xM3ceFhLQjw4NjIuKyorLCwtLzI3PUlbbvDf2dng8GhWTk1OTk5UYPLWyL+7uLSxrq2sq6urrK2usbW6vsbP4mdQRT47NzEtKiorKywsLjA2PUZOWXHo3tzd6G1eXFpVUlVcbObRycO+urayr66srKurq6ytsLS4vMHL3HFVSUA8Ni8rKSkpKSkqLC81PENLWv3g29re7nRrYlhRT1FZaenYzsjAu7i0sa6trKyrq6ytr7K2u7/I1fZZST86My0qKCgnJygpKy82PERPZujZ0tHW293i82xgXV9u7+DYzsjCvbm2s7Curayrq6ytrrC1ub3F0epdSz86My0pJycmJicoKi0zOj9KWXjf1dPX2drb4Ox9cXvr3dXOy8fDvru5t7SysK6trKytrq+ytrq/ydX1WEg9Ni4rKScmJSUlJysvMzg/TWTs3tvX0s7O1N3i3NPOzs3Iwb6/v7y5uLe1sq+trKurq6utsbnAxc5xT0U+NCoiISUjICMrMjU7Q2DtXFRZVkdJTlvh1tHJvr/Dv8DDwsLFw767t7S0sa+vr6+vr66zuLy/02ZqWUY/PzYoJCUgHiEnKC0zPEhJTGBdSFFZS1Phz8q/vLm5vr28xcnAwsm+ubi3sbGys7S0t7a1trm2u8rP4k9EQTk5LCYpIx4iKCQqMzU6SVNQWVxlT1Rla+HKv766tba8ubnFw7/Iyby7vLWztLS0tbi5t7e4tba7wMvlW0s8QDkqKiogICckJS0yMj1LTE9balRPYmdd1sLBvbS1ure4v8DEyMnKwL29trO4sLK4t7e6uLe2tbq9w+VjVz1BNysrKCEhJSMmLC8yPUtJTWVeT1hhXV/Vyce7uLm5t7y+v8nJxcvGvby5tbO0s7S2uLe2trS0uru/2mdhQD06LSwoIyEkIyUpLzI3Sk1HWfpIT3ZWUtLMz7y3vLe1v7292c7D18q7v7y1trWwtLSzura0ubW2u77J5WZHQzouLiojJCUhJiorMTo9Rk1QUVFTT1lZbdrWx72+ure8v73N3crN17+8wbi1uLSxtbS0uLW1trW3u7/L6VJHPjAuKyciJSUiKC4sOEg9VGpNXGZRW2Be7t7Nw767uLm8v8XT2s/Szb6/vLa3tbCys7G0t7S1tra3vcbZaUY6OS0qKighJiklKzgxPHhIWthVXH1QWmts9dfOysK+v72+y8zT7t7LzL+4uLWwsrCusLGxsrW3t7q9w8vuTUY4MC4pJiclJCgqKzU1QGVOaNNZVepNTXphWtvT28jBxb++ycvO39bOyL25t7Cvsa2usq6xtbO2vLu/zNZmTD42Ly0oJiYjJSgoKjA3OknkXnLMaFDrWErtdmDWzNjGv8C/vL/HxsTJwbq5tLCvrq+vsLKzs7q6usHJyd5cT0A5MC0qJiQkIiQlKCwtNEFIWMzi5MjyVN1fUtnf6cfHyL28vLq6vsDAvr66s7CvrKyurq+1t7vBxs3c5WJRST86NC8tKCYmIyMkJicsLzY+SPjb2srE58/GaN/E4tW7yMK3u7yzt7i1t7e3trOysK2ur6+zubvE0ddmVU5EQT06ODQwLysrKiYnKCYoLC0xO0JP/dHKwr++v8LExsfHwb6+uba4s7C0srC0trS3uri3ubq3u7/Ay+L5XklDQTo5NjIyMS8vLywuLCstLS0vNTc6RlBd3cvBv7y3ubu1ubu1ubi0uLe0t7ezubm0vL66v8LCxcXL1s/tX2BPRD89NzYzNDExNDUzNzsyOTo0NTs5Nz9FR1B18dTPxL/Aurm9u7i7vLu2t7y3t7u8uL/CwMDJ08vPd2zUXUhqXkpHTk8/P0Y/N0M9Nz05PDg9PEU/RlJIXlRM7FpdX9j729rTwufFxsjGvM3Gwcy5zs/CvdbJxO3J0NvL5dHZ6lPE2T/V1WdAbVznR0zVT2FIcE9jSk9dT1pQPNE/fl1E2UrXQMdJ6szhQszTSMtKvD7R3MdPTLA8z+i+VlHHz2xH175DTMXFTjuzVmVR7s5D4ky2Mc/KP8U1tDW5N3y3L7g7uT5exkziW9bENLzOQcw0sE1Awki/Ts1T9r44v1vQX1LIUMlHw19IyVXLPb//Q8Jbzj68Qs5PTLU65dBDuz/eTrYuu9JAb2G+NLstqjxBw2jBLsDPTkjHV8481tf9WVeyO0q1O8pWYrtA/MBwT261NMrFOrY8v1ta1N7EPMJQyE1PtjrKSspo+1Pcy1tOvULNT9veTvXtWla+O8pUz19Duja7Ql/DO7UyvM0qpCitMtm6MK8trUdFwlbsQ81IwkLTSLU/PbJSP7o9wk5HtzjCPbxMZcg4sT5lyvJtcuTPRsnsTufg8mbsZtjtUuS9OsnbTcpKwzyzNN29Obc9vzzKXcpM8U/ESVC4L7f/O8FDy1feQ8/DOcJGw05vespO2mJ042Re1NE6sTztz1jTTcY/uj5gxUXDO7E5ytIypitdrS/CTuzSU9A3rjlLszS+cE3VbX1PxktOsjPG2kvaUeBNw0ZYv2RH3sRIXcZAvDxXu0rRQrxiVtfebOlW39JFy0S8VD+yObgzxr42uTOuNlm+O7s828A9wETXyzq+TM5VZ8lNbcRKxk9dxDe7N7hJSrUvrzi8QM/fN6wu38xSz0O9PLhCTrtQPcrdTco5vWz2U+m/PsZH0NVBy89Z0kG5Te7y0/JPxjq3U0zL4e1MxlTZ/0zBT2v7UdFfZUvLVGTLSt3XUGDWaEm/SlC+R2DU6FHMRMT5T+JVzT66L7rwO7k8xk7QP83XP8Zv1mn4U8xJV2xh0E7j5ctcVr5P++hItzp68ND0QblGv1dOu0neUtfcUt1ezfpPzFXeUWvXW+tWy1Rk3WjZWfz41WVdyvfqb/Lo+2tW1V1ifOvZV2rP7kz26nRsXFrSY0vrzVLhVfTbSfNf2Vff3mTQT97fbXLg6PzsWuXgUXPtZutpX+PcXvzeYvVgVeRqZt/l79zb/tR9Z9tr8Wls6m72bOPj6eZ72vFkbXHpcmzf1/pwempqWlVgcldf+mVtZGr3amBu8Wljaurf6u/dztPPycXFx8jNzu1642po9nt0Y1ZXVkhBPzs5NjQ5PD5HTVNiZmrZzMi9ubSwr66trbG2vdtSRjw2NjRCaUpER0c8Mi80Ozg5QExfYWjfzNHb0cm+vb25tre5ubi6w8i/vcHLy8LE0Nzd+FhNSEZEQEJHSk1TYPzr5trV19PP0tn5T0M8NTQ4OkJQTE1OP0M+NT0/QFZi3sfBxL7Aycvn0snWzsK7vL/Bvb3Izs3P22VUXWNUS0dKT0tJT1JWVU9WV1JPVFdfWk5SW2nz7dfPz9PX0NLSzszFxsrFxsjM3/13Wk5OVGjtfuje7O1pZOjXzMjEvrm4u7y+w8rsaGlWSEBARUQ9PUVGQTs4PkE9PT5GTUI+R1Bfbmfh0+b+8N7Pzc7JxcfKztDNztLQy8rLzM/P093h7nFkXVlUVFlfYl5cWldQT1BTWV945NnRzcvKztbhd15UT05OTlBQUFBOUFhgbXzs39zd3NnX19jV0M3KycnJy87R1NXW2Nzh5OPg3t3c29zf5OXf4e18bF9XT01MS0pLSktNUldZWl5dV1FQUU9NTE9QTkxLTE1LSkpNU1pi+tzPysbDv727urq5t7a2t7e5u8DL2PhdTkZBPz09PT5BREdJTE9XXWh6/Xt7dWxscG1jXFhTUVdbW19scW508+DY1NPSzsvLy8rJycrN0M/O0Nng5+5tXFVPS0dDQUFCQ0NERkhJSk1QU1VXW2Blanb16uDa1dHPz9DT1tfa3uDj4+Ph3dnW1dPRz87Ozs/R1dvl8nRiWE9KR0dISUxRW19icO/p6/RzZmJneurbzsrIycnIyczQ1d3r+Xx4ffTv+3BucGtgWlZQS0ZEQ0FAPz9AQkRHS1FbafHZzcfDwL28u7u7u7y9vsDBw8XIy8/X5ftmV1BMSUdGRkhJSUpKSk1NTU1OUFVZYXPv4d3b1tHPzc3O0NLW2+Dk6PF4aGJfXVtZV1dXV1peaHvw5d7c19TV1tfa3ubs8nlrY1xXUU5NTExLSktMT1JUV1tdXVtcZ/bn497Z1tne4OPr/WhcWVdXVlRUVlVUVFdbYWhx9+Xa1M/Lx8PAvr27u7u7u7y9v8TJz933ZVpTTkxKSktLTE1OTk5OTk5OTU1NTk9QUlRXWl1hZ213fXl4+u7m4NzZ19jZ2tva3OHp8Pb8d3R2cGtmYGBiZWdnanP67efg3NnY2trY1dXW1tTS0dDOzc3Ozs7Ozs/S2OL2a11VT0xJR0RCQUA/Pz4+PT4+P0BDRklLTVBXXmd079/Wz8zJxcPDxMTGx8rMztHT1djb3N7j7P1vZl1XUk9NTExMTU1PUlZZW15hZWhrcvjo39rVz83KyMfGxcXGx8fHyMnLzM7R1dnc4OfxeWtgW1hUUE1LSkhHR0dGRURDQ0NERUdJTVBYZe7a0MzHw8C/vr28vLy9vb2+v8LFyc3U3/RsX1dPS0lHRUNAPz8/Pz9AQ0VHSk1SWmJu//Lr5OHe2tjW1dTRzs3LysjHx8bFxcbGyMvO1Nri/WVbUk1IRENCQD8+Pj49PT4+QEJER0tPWGFu8+Tc2dbSzcvJxsTCwcC/v7+/wMLFyMvMz9Ta4Ox6ZVxYUk5LSEZFRERDQ0RGR0hLT1RYW2Ft+uvj3NfSz87MysjGxsbFxMTFxsfHyczQ1Nfb4erye21iW1dTUE1KR0VEQ0JDRkhISUtNT1NYXWd0fvjs4NrX1tXT0M/Q0c/O0NTW19fa3ujz/XdpX1xcWlRQUFBPTk1MTE1MS0xNTk9RV11mbn306N7a2NTQz9DQz87Ozc3O0NLU19nZ2Nve4+ru8fh2bWtpYFtaW1xaWVlaW1xcXF9maWpvfvHq5d/c2dbV1tbW1dbY2drb3eDm6Ojq7/j9fHVsZmVmZmJgYGFgX2BiZWVjYWJkZGRmbHR6/fLs6uzt6+jm5+fo6evt7Orq6+7w8fHz8/Hy9vv+fHh0cW9ta2lnZmdnZmdpamloa25vbm1tb3N2d3r++vj07uzs7e/z9vf39/j39fLy8/Hv7e3s7e7x9vv8+/17dnNwbmxpaWhnZ2ZnaGhoaGprbG1vc3Z3eXp9/vr49PLy9PPx8PDw7+3s6+rq6urs7Ozr7O3u7/H2+vr5+fr+eXRwcHJ0dnh5ent9/fjz8O7t7e/x8e/u7/H19vb29PLw8PDx8vLz8fDv8PT4/P38+/r8fnp4d3d4eXp6enh3eHl6fH1+/35+fv7+/n58fHx6eHd1dHNycnN0dHNycnNzdnh5eHZ2d3h5eHd2dHNwcHFycXBwb29vcHN3e33+/Pn18vDu7u7v8PHx8vT2+Pn8/n59fn5+fX7+/Pv6+Pb2+Pn5+fr8/X5+fXt6enp5eHd3eHl6e33//v3+/fz7/P3/fX18fHx9fn7/fv79/v7+/358e3l4d3Z1dnZ2d3h6e31+//79/f39/f39/Pv6+fj49/f29/j4+Pn6/Pz9/f7+/v39/f39/f3+/358e3t5eHd1dHJxcHBwcHBvb25tbW1ubm5ubm5ub3FzdXZ2dnZ4eXp7fH19fX19//7+/v7/fn59fv/+/f39/Pz7+/r4+Pj4+Pf39vX19PX19/f29vX19vb29vb29PT19vf4+fr7/f3+/35+fn19fHx8fHx9fX1+fn7//v39/f3+/v//fn18e3p6e3p6enp7e3t8fX1+fn5+fn5+fn59fX18fHt7e3t7e3x9fv79/fv6+fj29fX09PT09fX29vf4+fv8/f3+/v7+/v38/f39/Pz8/Pz8/Pz9/f7+/v9+fX18e3p6enl6e3x8fHx8e3t7fHx8fHx8e3t6e3p7enp6eXl4eHh5eHh4eHh4eHh4d3d3eHh4eHl6enp7fH1+/v38+/z7+/v6+vr6+vn5+vv7+/z8/P38/Pz8/Pz8/Pv8/Pz8+/v8/Pz8/P39/f39/f39/f38/Pz8/Pz8/f39/v3+/v9+fn19fXx7e3p7enp6eXl5eHh4eHh3d3d3d3h3d3d3d3d3eHh4eXp6e3x8fX1+fn7///7+/v7+/v7+/v79/f39/fz8/Pz8/Pz8/Pz8/Pz8+/v7/Pv7/Pz8/f39/f7+/v7+/v39/fz8/Pv7+vn5+fn5+fn5+Pn5+fn5+fn4+fn5+vv7+/z9/f7+fn59fX19fHx8fHx8fHx8fHx7e3t7e3t7e3t7e3t7fHx8fHx8fHx8fHx8fHx8fH19fX19fn5+fn7/fn5+fn5+fn5+fv//fv/+//7/////fn5+fn5+fn5+fv////7+/v7+/v7+/v7+/v/+/v7+/v7+/f7+/v7+//9+fn5+fn5+fn5+fn59fX19fX19fHx8fHx8fHx8fHx8fHx8fHx8fHx7fHt7e3t7e3x8fX19fX19fX59fX1+fn7///7+/v79/f7+/v39/v39/v3+/v7+/v7+/37/fn5+fn59fn5+fn5+fn5+fv/+/v7+/v39/f38/Pz8/Pz8+/z8+/v7+/v7+/v7+/v7/Pv7+/v7+/z8/Pz9/Pz9/f39/f3+/f79/v7+/v7+/v7+fv9+fn5+fn19fHx8fHx8e3t7e3t7e3x7e3t7e3t8e3x8fHx8fX19fX1+fX5+fn5+fn5+fn7/fn5+fn5+fX19fX18fXx8fHx8e3t8e3t8e3t7fHt8fHx8fH19fX1+fX5+fv9+//7///7+/v7+/v39/f39/f39/f39/f3+/f39/Pz8/Pz9/Pz8/Pz8/Pz8+/v7/Pz8/f3+/f79/v3+/v7+/v/+/v7+/v////9+fn5+fn5+fn5+fn5+fn5+fn5+//9+/v7//v7////+////fn5+fX59fX19fX19fX18fX18fX19fXx9fXx8fHx8fHt8e3x7e3x7fHt8fHt8fHx7fHt8fHx8fHx8fXx9fX19fX1+fv/+//7+/v39/f38/Pv8+/v7+/v7+/v7/Pz8+/v7+/v7+vr6+/v7+/v7/Pz8/f39/v7/fn5+fn59fX59fX19fn5+fn1+fn19fX19fX19fX19fX1+fn5+fv/+fv5+/35+fn59fX18fHx8fHx8fH18fH59fX5+fn7///7///9+/35+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn59fXx9fXx9fX19fX1+fn5+fv///v7+/v7+/v7+/f3+/f3+/f39/fz8/Pv8/Pz8/Pz8/Pz7/Pv8/Pz8/Pz8/Pz8/P39/f39/f39/f3+/f7+/v//fn5+fn19fXx8fHx8e3x7e3t7enp6eXl5eXl4eHh4eHh5eXl5eXl5eXl6enp6e3t7fHx8fHx9fX5+fv7+/v39/Pz8/Pz7+/v7+/v7+/v7+/v7+/v7/Pz8/Pz8/Pz8/Pv8+/v7+/v7+/v7+/v7+vr7+vr6+vr6+vr7+/z9/f7+//9+fn59fXx8fHx8fHt7e3x7fHx8fH19fX1+fX5+fn5+fn5+fn1+fn5+fn59fX19fHx8e3t7e3t6e3t7ent7e3p6e3t6enp6enp6enp6enp6e3t7e3t7e3x8fHx9fX1+fv/+/v7+/f39/Pz7+/r7+vr6+fn5+fn5+fn5+fn6+vr6+vr6+vr7+/v7+/z7/Pz8/Pz8/P38/P39/f7+//9+fn5+fX19fX19fXx8fX19fX19fX19fn5+fn59fX19fHx8e3t7e3t7enp6ent7e3t8fHx8fH19fX19fX19fX5+fv///v7+/v39/fz8/Pz8/Pz8/Pz9/f39/f39/f39/Pz8/Pv7+/v7+/v7+/v7/Pz8/P39/f79/f7+/v7+////fn5+fn19fXx8fHt7e3p6enl5eXl5enl6enp6ent7e3t8e3x8fHx9fX19fX5+fn5+///+/v7+/v7+/v///35+fn59fX18fHt7e3t6enl6eXl5eXl5eXp6ent7fHx9fn7//v79/fz8/Pv7+/v7+vv7+vv6+/v7+/v7+/v7+/v7+/v7+/v8/Pv8+/v7/Pz8/Pz8/P39/f39/v7+//9+////fn5+fn5+fn1+fX19fn5+fn5+fn7////+/v39/Pz8+/v7+/v7+/v7/Pz8/f7+/35+fn59fX19fX19fX19fXx8fHx7e3t7e3t8e3t8fHt7fHx8fHx8fH19fv/+/v7///37/P36+f7+/A==", "UklGRpRRAABXQVZFZm10IBIAAAAHAAEAQB8AAEAfAAABAAgAAABmYWN0BAAAAEBRAABMSVNUGgAAAElORk9JU0ZUDQAAAExhdmY2MS43LjEwMAAAZGF0YUBRAAD5+vr8/f39+/n6+/3/fn5+fHt9//7+e3h4ffn4/nRsa3B8+Pt6cHB+8Ort+3J49ebk7/1zfu7t83Rpd+3d3OhyW1VWXvvi6P5gWmp37OloYG7dysvb8ffRz95YSU7t0vRBLi4xW8W5vz0wLD3PvsDfZmpQ7NC0qq6+Qzc/za6qrtNX+7qwxz8nKk6unqVmJxsgP97KOCQrRLerzmlEzqKwvDYt6aqfpd0rJzi1paa0VzAvMkVURkA4PVRPRy8nKC73tq6v1E45Pce3sr7Ht62qsXo0Lj21oZykyjIpMUlNQjMxNDw+Ni4wVL6tu24uKDy7o6KyVD5Yuauwyj8+1KujqL85MDdS3E4yJh8nL9C1vtA0Ly47/L63uL/Bv7m85UxAYLSppKnEWTxBbutePSkkHB8z36evwDUlKTfIrrC6ztC0s7brQU7MrKaor9JORknuTEQtHh0fPbemrNYvJCc9v66yw9DOurm9zdzJu6+qrbjMT05GSTwnHxsjPLWoq88zJSo8wLGyu8nDwLq/wcvDuK6usMN6RzxDLykeHidDs6mu3S8nLEi7sK+8wszFvbq6vr20r6yyv+8/PCwlHh4mQ7eqrMw3KSw9y7Wyt73Du7eytcHByreztbhuTi8lHRwgMdKtq7dWMC02WMG6uL7Dv7eysLi6v7u2u7xeRywjHRwjN8Wurr5JNDI/4L+6vL6+uLCur7i5vLa3vs1CMyMeHB8ubLavuOc/Nz5UzcTDwsK5sa6tsra4ubfFzz8vIxwdHzFku7fEaENAVeHHxMnGxLqyr66xsbK0uc5NMSQeGx8nQ8q8vN5dTV7UysTHyL+7s7CvsLCwsLbFUjMmHxweJTdvzcXr6XPPv729ydDNwraxra6vr7O60UYuJB4dHicyWPTT3ePNx7q6u8HKysG5srCvsLK3vuRJNCokICElLDhLZufQyLy0srG4vcTAvLq5urq6vcHXX0M4My8uLzA0OEJR+d3RycG8u7y/wMK/v76+xL/Jzd97e1tSUkRDPz5APkJGSVBVWXXh0s7My8nGwMK/w8LDx8nLz9Xg5ftoW1VNTEtOTk9RUVVaXV9gY2Rrdf317Ozo4+Pp6+jr7O/5b2xjYF9dXlxcWlpbXF1fYmRna3j99fbz8e3q6Ofm6enq7vX6fnJxc3Rydnt6fvf29O/x7u7r6+rs7+7v7Ozr7e/z9Pf6fXh1c3d4e3x+fH79+vjz8fDt6+rs7fDx9Pf+fHdzcG9vbW1sbm1tbGtsa21ucnJ0dnl6e3p3dHFwbm9tbGxtbW1ubm9wcXFzcXBydXh6/v/+/vz8+v59fn17d3VxcnV3dnV3ef7+//77+vr6+fj39fX09/bz8vX49/X7fXp7fHt6eHx+/fz6+frz8/L28+/w8vD18vDw7Oru7+/09/b3+H39/P57/f19fHt7efh+/nl6fX37a/9x+O1pYu51dHZf7Gt6Z+9d/PxjcmJ6+mhpcWZm6GFte2LpfeVsa3n1embsYG9dzXLo0lz64Vdz6l7QSN1Q2t58Ymno11bd6Vzuevjbcttf3+nTVexzb2vba2viU9rl/dlc2Ojq1/Tp4NpN0+Pya3zWYuDiV+d09HD4Wtp1WfzOP7ozvUZYxD/NPMY+xUjY2mPqXvNJ00naVFPiR8pU011dyUjIUcxFvD3R0lbgdnNa3E3KU2jQTcRAvl1VwDq8Pbg4tDS+cXJNvkLOVVfAO7VK0E/+4lfMT1fIS8hLzU/ey0/IR7Y+uk9Quj7LX8xeR7s7tkXDNrA59rsrpyWpKKUmslQ4rSiwO8pMvjKvMrpNVedAyzi5NbE0tDK3V1HHQe9d0D+2PNTCKp8hq0NivDvRT8RBv0fM3ki/NbRHULZKxlduwF/WYL47y1xcxlDd8tH4OKkqrC7JzTnOQLgxrCGlOT67OMBBRs/KOL474MZD3Fnu7eE9rizAUVvMd1jT0zSuLLtP2Np05btNbsE1tC63O8duRLY3tji6StBLzDuyNb43sjjdtzG91d9bsDS73kXPaejGej6tQM7Jcr9M7vbKR9tDvm1Cvky9Pc961ES+OsHnK606yF3cRK0nyr8wvFMytU8/ti/a2Txf5kXUxDq7S9/DOs1TZjzDPMBYTLpe1fPceM39QL5XzXBT18laSrX4WsFgz2BpTsffRsjUblls1MZAUsx8V+xKv21IxuZW5G1lwkd7yf9b73Xc7l1U3lFLcnnlYVPP2DnsZlffTULHRz/z8eHrQOzYPudX2Hvf5HDVVujr7+vfY+NXWe13T/Tma9dubOfJX+bkVtdlU9Vw0L1P4MVj1eBI3uVR3V191krq0GLN2lvm3U5eWlTidF/Ry1RU69ff+WfZyuXfx8bYeuJvTEU/SkA4OUhcTEVN37++ua6rrq2zr6/Bsa65wMZNLyskKCgnJygsLjM607Svq6aeoqimoJ+s0D06KycsOzs8Qy8oJSEeJScu47yup52bn6eioqe5NC81Lyw6et1ZTDotIB4fISgtZrCrp6ChpaiopqVNLTowLC8+1bbvSz8oIR8fIysz2ayrpaCkqKyqpKRcLkA3Ly4617G77EMuJyMgIyc0vayrpJ+krLmvpaLdLFhFLSo/076/x8k3JiElJiEl7qqvsKKfqLvAq6GrMjDrPCguS76308TONCMjJigkLMCorq2goK7DuauitDY1yjsoNVy/w8nu3TUkICgqJDPIsKyrpqGrv7arpq5MKlzcJSpHxsnMYt9YKx4mLiMqYr6uqqqjpre0rKmozCs7vSslPtrL0GNx1i8hJS4jJDzlwK6qpqKrr6yrqKpDLcZsJCtKbExOVdxLLSUsKiAmRlDstKqlpqqqqKqoqPMv3HopJkBOQE7/61o5LC8sIyU4Pj/Aq6mopqSlqqimvz1MWy4qOUY5P1r/aEw+OTUsKC03Nz7gvLKvrKiprainuMvC6Tk4PDU0OTtBT0lGVlA+Qk8/OUBMVWRx0Ly8wrixtru4ub7Fy9HY8V5aWlJOUFBPVVxcWVpYVlZYWl9sfe7l3dPOzs3Lzc/Nztjm7PVsX19fWVNVVlVWWVxganF1e/jx+nv57/Ds39rY1dLS09TZ3uHn9Xp7enFyfP3+//z5+35+/Pz69vLw7enq6+rr8PPw8PP07+zu7+/x+X14dnRwcHR4eX379/Tz8/X6fHRwbWtpaGlqaWhnZmRiX19gX19iZmhqb3R2enx7eHVxbm5ub3J4ff37+Pf6/Xx8d3JwcHJxdXp9/fz7/P98eHh3d3p9+/bz8O7r6+3r6urs6+rr6+no6Ofo6Ons8vT3fXp8enh9/P769PT18/L08/Lz8fDw8O/u8O7v8/X1+v99fHd4fHz/+/r69/X18u/w7+zq6+zt7u/x9vf5/nt8fXp4eHh4eHZ0dHNydXh4eHp8fX59e3h2c3Fxb21tbW1ub3BxdHZ3eHd1dnd4eXt9fX1+fX17enp5eXd1dHJyc3N0dXd5ent7ent7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2kk6NSskIx8dJh4iLy0yb+PRuLCzr6qtsauus7CytrW5t7i8uLy/ws1+VUY2LyskJR4lJSErMzI+13fFu7e4s7GztrC0tbS0ubO7tru6u8O/zMzr5UxPPz0zMy8rLywuLTUyOzxKTl162tTQxsfBv7u9ubm6ubm7vLy+vsTCx8zP1+pvXU9JQj47ODc1NDQ0NTY4Oj1ARU1WYvPd083IxcLAv76+vr6+vr/AwMLFyMvQ1+N7ZlhQTElGQkFAPj8/P0BDRUhLTVNZYGv96uLb19PRz83NzczNzc7Q1Nfc4Oj0e25mYV1cWllZWVpbXF1eYGRmaW5xd/348+3r6Obj4d/e3t3d3t7f4eLm6Ozu9vt8dHBwbm1ta21tbW5xdnt+/fn28u/w7u7v7O708vd+/f13e3d1dXVzb3BubWlsa2hqbm5xe3B2fnt2fntvcnBtcnd3cHV5fvz37+7q5+nm5u3t5+Lf8e/p5/b4eX76ZWtyZV5lcnFzaGlyZHVpa29jaG14Xeflau319ePz817xfV/KZf3t3fdzzE9839vNWb65TL1TQU1V/edw3kVQ2L7hvNrt0LvAQsXXW0/PTjw7TU0/6d3tVtP+yd1rSms1SDpGRElR2VBdX+ZJ2kjYcPbTvty92cDJzWy3aczNwuHp28L258DKzXjCz9dhzc0/3edO8lh531JKuTrreVY6zUVST1NQSFLjRVDfTTzPRWY56lpIOMhRPkrtSkVM30tIXc1JUdViXFTN4e1JxeRI4sVP02XJyE3Ys1NivuDUSMLGWFi40dzTysXw18vV3OjR097h1cNq7cHNTdfO4UB3v1JHyclKXcxtTOPNW0fVxEhNvNxP+dPfV0zMc0DPXUflU1TdX93iWdNWXdBGTMtPSftyWmbW20rZyktLYV9DPlt6O1PiZEto12NI09VIXuNKW1NV/WRcbeHO52nO3O712tnk28vN2NnX0tj53OLrWFdbQ0RgQENkR0FKP0NFRVRQSVxFQFNMWWxpwsTJurW3s7mztLOwtrquuLmyvbq/WPNMMDQuKCclKCYkKy0oLDI0MDE7NzBBSkHYuLCpoZ6bnJ2bnqKlrLe2vdTe2WtUQ0c9LiwuKiMhJCgmJS0uKSktKykrLjIzOlXwyK6ooJuampqdoqStvrbE37/AysHec+VNNTMuKSonKi4tLTc4ODQvKyQiKCgsOkbiua+onp2cmpudoKStvcK/xs7As7G7yMV2PzIvLiksNDQ5REBMQDU7NScjJB4eKz5E6LampaWfmpygnZ2ns7262T1Kv8HewrW63zg0MCUiJywvNkbh0lk/QS8mJiogHCApLjvHraypp6OdoKafn6akufDD4kdZd8i95MC2Yjk4JiInJCkwOWTIzdlsTTcoIyYkIicsND96vK+srKikpaSioqGgoaa7Kj/IOy9Mzq+4Xca3RyMfJighIzbz2dLCt8k7Ly8sIiItNjQ2Ss2/zsGwra6rpqCdnp+cnadWHivOJSZBv6mszq6sSCUeICYiIjnhxb3AuLtPMCwuLykpP+peW0xf2UxH1rWwrKignJycm5ugwyUdOT8hNWCvo7DBr70zIRoeJysuPd+zr77V/T84LCoxNThU6M/bST5GPkBO5bavraahnpydnZ2kwisbMG4pNT3An6m8vdxALRoaHyo8SUi9rq7APzI2Mi8yMkjKzNvpTEQ4LC84RdS9ua6qpaKhn52bnKsqKUo7Wy4rfKusq8BvaDkpHRwiNUNbTNi7ucpIMS4zODs7Tt7Jx3RGOC8vNTVAX9vJx8q/tKynoZ2RmLDlL0nSSC8tLr6rqK7iTVo7LCEdJDRW3FpG+9XbRi0rNU3Vz+DNxb/VPi0sMDlCUGLnX01ZO97Q7rOYlZCatM1K1sw/Mi48sammrc12Tz84KiEjKz1nTjs1PEhGOTQ7Tcy8vb/kTVQ+NjUsOT9er95fPjVIXDg3RLeak5OcrsbGxsRNMzFDuqinrcDq5d9gPCwoLDlGNyojJC02RsZJ2LiwpbhOLSY3TfdkNjZKza2vvlsvLzI7S+u9qZ6am6Csvtd5blZTZ8q4r66xuclhPzQxNTk5MCclHyYoLT0sO92snp6u5CwrMT1PPjk9Vryvr7lkPDM1PEh7zraon5yepbLI3Nzb4f5xz7qtqq231FNBPj49OzIsKiMmIiglHyYwxaafn6vOTjpCSDw5LjA9bbuzs7fI3l1IR0ZkxraqpaSjqa2zvcXT5u/cxbq0s7rF5lVLQDsxKiMfHiAhIyEgKjrBrKirtM/+WWdjRjwzMzxN2MC8ub7Ezd3g39LEurGrqaipq62wtLm/yM3NycjM1e9jV09HPTMsJiIgICAgICIpOeS6sLO6yOFtW09GPjs+R2TVyL+8u7q9vsLHxb+5sKyqqqutrrK0ur7I1eRwY2FYUE1FRj89OTUwLiwrKigoJygrLzhCT2Hr18vIw8XK0dvc0MnCvr28u7q5ubm6u7y7ube1tri5uLa2uL3Gzt7sXlNNRD48Ozw6Ojk5ODk4OTg5OTo5Ojo+P0ZGTFBWYGnw6eTc0NTQzMnHxcG+vr29v7/Bv7y/vsTAx8XFx8/R1dTu7ntpaFtfY19ZUFJUTUpMSkdJS0tJTUlOUFdOT1JUXVluXXtuce7f3efp2dXZ1eHX39Te1ujb497a297j6u/o6t1s62BwbGp2Z/Bm9nB5Xmds/mVlYmtneWp2/m50bXj0dfB7+u3o8n5z/uxz83puaezebW726vpwfPx1evNuaflx7O57b2Z182poanBu+mlt+G9vbXNrfHt7/ftz7Obrcub0ftv0+O3t4OLo7O7x9tx91/XcY+T26Ovj7XB39+ny9efw5+3r+nx6+Xrka/5odXv++mJ8YetgZGf54O9ZYfz3cVhsV2lkcFpnYVt3delmbXrw9V5paF5f+uhqYuHu52RocXV77/hm8232dmT3Y/lsaXTtcPngafBg32Pab9t81+NdcVzibmpq69ze3Wzt8Ob73eZee/bY9uz7WmHk++lfaeZ823hzYdxjZ1nqcepU11bRWXFg237o+E7yaeRC20vFS/R063dI+2voVNtX5lBeYNTo6NhiYeNb3l7u29PWzOnp0s3k2/Bix97Qad7Uyczg333T8dTMWOJn1u3sXtna5d7vbet75m5aX29w8vNRemjoZ1prUt9YbWte4FXuflHQTdxQcPtS2l3ZVXL8WGdLd178b2Tq3l1k531r62BqYGzsXmNv6mnganfab+zj3ent8OXt4vBn8Oh09npw4+325d3z6XV45XDcdfVlYubf4mX/VfLk7FpeXe5ncG1b5P5UYHX7+Fpf+O95a2h1bvB8eXhtfGHy8275+PDpcPZ84/B653D2dHjt5XX+4Xby9O7faXbZ/nF56e77c2np/Gvtcv3icXZ36Xx27u56+/L3eHZub+9nbXH18W577W314G5pcfju/+xubOr85PRv9nv98fJ2ben7cON2+23r9Hd4d+9s+Xj5cfl4//11d2j3cn19fvZ+cXP9+3B5fnB4a35ydfxyfmjsenZrbn567GJtfv78bXtj///5+3Fm++9u9/JtafP0fXrw8W397/r+6HzxdfD0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wMfCy9jT3FhTPS4wKR8fIxwcJCMiLz49d7+9vLS0uLu7vcjKy87SzcnHxMK8u725trq6ubq+u7y/vLu6ubi6vLu/yMrL31xKNDUqIyEgHhwfISQmNT5E9b3Aybe5yce7ys7Bw8G+vru4wL21vcS6u8fEv8bFwMa/vr65t7q6t73Cx8zX8/E6OjcoJCUhHB8iHyQqMDhCZ9LLzsC+0MW+xcG8ura2t7Gzvrqzvce7vMzLxsnLzM/HzdbEwcbFw8DG0tPW0vxLUUQ1LCwrIiMmIyUoKi81Okz+6M/Cw8G9u7q3tbOwsLGws7e2t7q6vL/DyMvNz9TW2ebn3NzYz83NzMzR1c7X+W5bRjw4MSwpJiQjIiMmKCswOEFV4sq+ubWyr66trKysrK2vsrW4u73AxcrQ1NXZ3ubue21uev706eTg4OXj4O9lV0xAOjUwLCglJCMjJSgrLjU/VdvGvLWxrq2sq6qqqqusrrG3u7/Gztbnb2VgXmRnYmdwbnHr3+Hp6/llW1pYT0lBOzYyLiwrKCUkJigqLzhAUeLGubKurKqqqqqqqqutr7K4vcbQ3nFVTk5MTVJZZvvq3tbRz8/Pz9p0XVZKQ0A+OjYyLy0sKykoKCcoLDI5R/TKvravq6mpqKiqq6yusLO5vsXN329bVVBNTVNcZXbp2tLPzc3NztXj+l9MQj46NzQxMC8tLCsrKysrLC8xOEr4y7uyrquop6alp6iqrK6zub3Dzu5fV1JLRklSVVFe5Nrd2tDP2Op6b11MRD87NTIwLy4tLC0sKywuMDI1OkhH8Luyraijo6Omp6ersLO3vMnX+uz1VU9YVk5PWPjFzcy8ucPP1N5sQDk9Ny8wMzUxLS4wLCkrLCssLi86R07bvbiuqaGbq6ifrsq4ub27yLiw2OrB+0VbY1JcWM+7yMO2u9HY3E9DQD48NzI4Ny80NzAtLCsqKyksMTA3PkFYztTFr7KuqJ+dta2luVvEtrrNxa+7VufKTUJq0ttczbi/yru4yGVa9k40OFE6MTg7NjAwNzApLC0rKy0vMzQ4RUtVx729tK+qr5+cs7KntE7Svbi+y7Sy5UzpUDtG6M/g6Lu40ca6xW5r6GRBO0xEOTg5Ny8uMzUtLDAtLS8wNTQ4PTg/VunGuLq1qa2pm6PEq63WWMu1tcO/rb1NTk8/O1jKzN3KuMlnysjw8fvyakZEUj44ODUxMTMzLzEyLi4wMzIxMTg7NzxazcO2tbarq6eao8ipq8xpz7Swx8auvFFbT0JARPPJ09a/xW5g6c10VNnmTUxMPTMuMzk1LzQ5LywsMDAwMT89Nzw/QEDXvbixr66sqqKaptCsrNVX0La3zc2wt09JXUtAPG+9zVvYxlk+TOF2T0zZykk8RDkvLjVDOS8wNS8rKS47ODZGRj4+RUjjtbu7q6a8tp2dpbq/rLNp47a6zsu+tsVMZeBJQln309V34nw7NktdTUpoy9pLSkI3NzE1SkgyLjIuLS0vPkU9QnvpTUNayry7t6ymtLygn67NxrC05FW+vO9r2MK/8E7jfkREVP3f81FSRDo+PjtJXFpleVpIQTk2QEs+ODY1ODMxOkRLX9fD1FV00MnBuLGqrbe3tqaozle/ssHoSO3KW1LUxM/3WFhVQUZmY2piSj05Nzs+PkJQ3+FSQ0VLRzw3PkU9OT0/Pj9D4rS2dGPKv8bcya6osr66tbW8vbS8adzI2XhIP2DT5tvW7O1ZQ0hQUW1cREdNQDcyN0RJSVV+7E87QFBEODpDTVQ9O1BIV7Cuvs7lvbTCzbmurLC/wLS40fzHtbjtPEzK7z02SsjFXj9L4NlSPUNecVdDQ05JOC80Q1dIRUtfYko9NTxIRURGT1E+M8ekrL3SvayuxMqyrKuvvrqxu9pTW8i9zWldWUs9Oj5TYUlGS1nmVj87PVjna05KR0c9MjdCR1JNSEpRRzIyP0s4Q2Q8NjPipqW52sStpKm6uq6qqbK/uLG4zktKzL3aTlfzfEEyNEJaWj85UV5GQTs7R2Dqy9pIPTk7Q0VBRlJ6ZEY9Pjo6MS5t9DgpKeapp8ZOzaugq8bHsKeotcW1q6y9WVTNvsfYef/Zaz84OkZGPT1ATm1OOjY6Tezual9lVUU6O0tcW01MX1c/Pzg4NjH56zYmKk20rMj02rimqbXAt62ss72wq6y112Xjxr/D3ubS2d9LOTc6Qk1OR0REPjo+QklMU+HM60o9OkZXXU9IWu5hRDw3NDtOx0EjISzOssVNQ+Wwpq7Dzb6sqrK7t66rr7/a383CxNLby7+90D4xNkvQy19APD9TYU1FRmbOzd1NPT5LWFlOREVNWlVCNC03SeBZJx8lOr+/Wj1DzK2ru9Hbu6uprrq/tausueBez7q0usnd387hTD9CWtbaTj09Snt0TkpZz8DPVz48S/DgYUo+P0ZFR0M7Nz5SfkclHiQy081hS0N+u7W9x9O/rq2ttcG3rauvyWLyw7Guts1UUm/b0vpPSEtWXE5AOztH5sXC20g8P1vKyOtKPkNNZGVBOS855M3oLx8fJjhi+UQ6O1bAvsTIyb21sbK5vLWurbTJbf7Ds6+3ymVg1766x1k+PlnOzmE/OUfQubjWPzg/8sXI7kpAQlBfTz4vM0jUv08uIx8lL0RPRjw8RFLRwr+/wsS/u7KsrK660OTSvrWyt8Ha/tXFvb3PZ1JX3tPvVEVJ5L+5vuFLQ0n3y83rUkBDSk9TOjdAY8l9OCYeHyg8XWU/MzM7+cS9wNLe17+yra2wucPMycS8t7i6wsrHxsTI1uDi3dHT81tSaMu+u7/YaFFU/tzT2O1aTktGQDg8T3zkRjEmHyAmMkJPTj87OT5P3cPAxsnHvbWvr7S9zNTKvLSytbvFz97p49bMx8jR9U9JVdvAu73F0uLs+nnz29bebVxfTEVASXv2fD8sIx4gJy89RUQ7ODc8SmTUzMLAvrq3s7O2u8DDv7q2tLa8x9v3+N3MxsTI0+1cVF/bxb29w87ncGBgcfDe3t/8VEtMZOviaEIxKCMjJy01PD08Ojo7PkRNZ+POxby3tre7v8TEvbiysrS7yOJhW2/Tw7u5vMl2T0tb2cK5t7m/ze9dWWDs2M3N2nJYXV9aSDkuKScnKS0wNjk6Ozs8PkBFTmzSwLm2uLvByMrIwLy4t7m9ydr3/dvLwL28vsbP2tzUyb+8uru9xtTtbXDw3NLW5mJUS0E6My4sKyssLi8zNTg7PkBAQ0ZNXejNwr29v8TIycjDvry8vcLJzc3Kxb++vb2/wcbJycbBvry8vsTN2+r07urs/mVXTUdAOzYxLy4uLy8yNDg6PD4/QEFESVBj7NfNycjJysvJxsLAv76/v8HCw8PCwL68vLy+v8HDxcXFxsjM0dvqcV5UTkpHRUNAPjw6ODY1NDQ2Nzk7PT0+QUdOWWVv/eze1c7KxcLBwcHBwL++vby8vL2+v8HExsbGxsfIy87U2+T3bF1UT0xLSUZDQD8+Pj4+PT09PkBER0hISUpOVV9t++zm39nSzcvIxsXFxcXGx8jIyMfHx8nLzc/Q0NHV2t7g5u97a2JdWllXVFBNTEtKSUhHSElJSUlJSktNUVZaW1xeZHHz5t/b2dbTz8zLzM3NzMrKyszP1NjX1dTX3ur4eXFvb2xmYV5cWVVRUFBRUlJSUlJTVFdbXl9lc/bz9PHt59/d3t/g4+Pd2dnd4ujo497b2t7n7+7r6ejq7O3v8vPz+Hx1c3J0c21pZ2RfXFtcXV1dXFxcXFxhZ2dnaW1ubnB2//Xu6unp6uvr6+zs6+rr7e3u7uvp6evu7u7s6Onu+3h4fPv4/XdsZ2hrbW1tcHBvbGttbnJ6fv7+/PXx8PP49/Pw7e3u8fX08/Du7/P08O3u8fTz8O7u7u/v8fHw8fT3+Pf5/np3d3RycXFzefz4+n14dXr8+f18dm9sbnJzcW5sbG9xcXR8/P39+vj39fb6/n16d3l9enNvb29ubWxsb3V2cnN5//nw7Ovs7vDw7e3y+fv9fXh1cm9tbnFxbm1uc3r79/n7+/r59ff/eXl6eHd4dnJydnh5en3+/Pr6+/n18/f7/Pv8/v39//78+/z8/3x8/v18d3d3dXRzcnBwcnV4e318fP/7+/3+/3x5e//8+/r39fTz8vP1+Pv9//5+e3l7fv78+fr7+/j29vf7fXp7e3dzcW5tbnJ0dHRzcnNzcXBwcXFwbm5ucHF0dXV0dHR2d3l6e37+/n17fX5+fXp4d3h4enx9fn79+/n5+/r49fb39/f29fX19ff5/Pz7+fj39vX08fDv7u7w8/T2+v59e3t8fHx9/v38+/n5+fn49/f3+Pf19PT19PX3+fr8/n57eHd2d3d3eHh6fX7+/fz6+Pj5+vz9/n16eHV0dHNycXJydHd5enx9/v37+Pf18/Lw8PDw8vT19vj7/3t4dnV0c3JzdXd6e3x9//37+vr7+/r6+/3+/n5+fXt5eHd3eHh5en3//f39/v//////fXx7fHx8fHt8fH1+fXx8e3t8fHx7e3x8fHx7enl6eXl5eXl6fHx9fv37+fj4+Pn5+vv8/f7+/v3+/f7+/f7/fn19fHt7ent7fHx8ff/+/Pv7/Pz8/f59fHt8e3t6e31+/vz7+vj39/f3+Pj5+vz+/359fHt7e3x9fXx9fv//fn19fX19fHx7e3p5eHd2dnZ2dnd4enx+/vz7+vn4+fn5+vv8/f7/fn5+//79/Pv7+/v7/Pz9/v9+fn7//v38+vn49/b19vf3+fv+fnx6eXh4eHh5e3x+/vv6+Pf29fX29vf4+fr6+vr5+Pf29vb29/f4+fv9/318e3p5eXl5e3t9//38+/v7/P3+fXt6eHd2dXV2d3h5e3x+//7//359fHt5eHh2d3d3eXl6fH1+/v39/f3+/359fXx8fH1+//79/Pz9/f5+fnx7enl5eXl6e3t8fX5+/v7+/v7/fn59fXx9fX7//v79/Pv8/P3/fnx7enl5eXp6e3x+fv39/Pz8/f7/fXx7e3p5enp6e3t7e3t8fHx9fH18fX19fX7/fv9+fn5+fXx7e3t6e3x8fX7+/fz7+/r6+vr6+vn5+fj4+Pj4+Pn5+vr7/P3+fn18e3p5eXl5eXp6fH7//fz6+fj39/j4+fv7/P3+/v7+/fz7+/r6+fr6+/v7/Pz8/Pz8/Pz7+/v7/Pz9/f5+fn18e3t7fHx9fv/9/fv6+fn4+Pj4+vv8/n58e3p6eXl6eXp5enp6e3x9fX5+////fn5+fn5+fn5+/37/fn5+fX19fX19fHx8fHx7e3t6enp6eXp5eXl5eXl5eXl6enp7e3t8fHx8fHx8fHx8fHt7ent7ent7fH1+//79/fz8+/v7+/v7+/z8/f5+fn18fH19fn7+/fv6+Pb18/Lx8fHx8vP09vf5+/z9/v7/fv9+fn59fn5+/v79/Pv6+fn5+fr6+/1+fXx7enl5eXl6fH3//fv6+Pf3+Pj5+/z+fnx7eXl4d3h4eHl6ent8fX18fXx7e3p5eXd3dXV0c3NycnN0dHV2d3l6fHx9fn1+fXx8fHp5eXh4eXl5enp7e3x8fX7///7+/v39/Pv7+vn49/b29fT19fX19vf4+fr6+/v8/Pz8+/v7+/r6+vn5+fn5+vr7+/z9/v7+/359fX19fX19fX1+fn7//////35+fn59fH19fX1+fn79/fz7+/r7+/v8/f5+fXx7enl5eXl5ent7fH5+/v38/Pv7/Pv8/f3+/n5+fn18fHx8e3x9fX7//v79/Pz8/P39/v99fHt6eXh4d3h4eXp7fH1+/v79/f39/f7+/35+fX19fX19fv/+/fv7+vn6+fn6+vr7+/z9/v7//v7//v79/fz7+vr6+fn5+fr6+/z9/v9+fXx7e3t7enp7fH19fn7//v7+/v7/fn18e3p6eXl4eHh4eHl6ent8fH19fn5+fn59fXx8fHx7e3t7e3x8fH19fv9+//9+/35+fn5+fX59fX19fn5+fv///v7+/f39/fz9/fz8/Pz8/Pz7+/v7+vr5+fn5+fn4+Pj4+fn6+/v8/Pz8/f39/f7+/v///v//fn59fHx8fHx8fX18fXx8fH19fX19fXx8fHt7e3x8fHx8fHt7e3t7e3x8fHt7e3t7fHx8fX19fn5+fv////7+/v7+/f7+/v9+/35+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fv/+/v7////+/v7+/f39/f39/f39/fz7+/v7+vr6+vn5+vr7/P39/f39/v/+/v39/n59fn5+fX18e3t6ent7e3p6ent7e3l5eHl5ent6e3x+fX3+/v3+/vv4/f35/v/8+/n39/r59vb7+fx1eHxyc3h5emlv/mxv9XVs7eDVz3dP8uROu083ulViXUXBQ9HBQdR7XMpr/HpRWNnK0s3QT73TRtRNTmtOZN9M9GZeWfrcWdZW1+1dzlbhVP/vRt5Oc2lg5fx81lfZ1VDMZWn2a9RT69NOy0O+T1++PrxPd9hb12Btz2P+2U3K61LLzmXeXcdkb9p8Uc5D1dU+vD7IeTywMrY40so3wknOSLorsFg1sza/SUi/TMtA0cs4sjznvDvGVbsvt0VPuTbB6ORIw09SylpbwDevPHS3LKwrtThssyOjKLtcOq4xv1VYxEzeQ7hOO7A/WNTeRLY+aLpFXc1w3PpFvVbHPrdsR7s8ukN7TtDmN71H3e5P2dHvSdvBOctnd8BCx0u9OtDNPLZBWL1M1eptXO5dPc9PRc4/4HvbV9veWchE2ORfUM9S9O9JznneVtP94l087llDQV1JRFZNcG9bz9zEwsC9uLawubqzvbXByMT2W0Y8KiUeHxocKC05O72ssLWxrr/P4NTaXNu/t7OqpaSora+5215UOScfGBopHh0sULu0s6+hq73Aa1NBODpO/OSwrKymo6KpuLzC2U5PUzYsGyA4KyIq8reszbulrbpl+FZ4RjrY2rCzuK+nprK+1ctkP05YRi4bGTsqISpDqaa+vqervEs4UPc7PWm0qbK5raauwExRVTY2P0s7KxchRSgrLsyipte2rbC8OznW0Enjw6uqvr6ussJXPkU7Mj9SOTAdHT4uLT3Op5+5vq23vE41U896176yq7rLvLvDWDwyNTY9Sjc2IiMvLDBQu6agsbWxub5wQGPN2Mu3s625+M7I0k03LjM0OD4zLiIlLzg8466kn6y1tLXB2kthy8u/tbK0vOTW3lRBOS4wMy84MygkJC1AS+SrpJ6msba4v8704721sba3uL34bU8/QC8vLi0qLCwqJiAoN2HBq6Sfpayxt73AycS6sbW1trm5xfNZNS4tLC4sKSksLConKi9Iy7apoqOmrLS6uLq9tbS0tbq7w8zZVkA6Ny4qJysvKyclJy0vM0LutqelpqitrbK8tri0sK+tssG+yNJmQjc5NjUtKyorKSYlKDE0Q0rlxravraqrr7OzsKuyr7Czt73Hws5nYFNeODYtLjAtKSkqLDEwPDxKRF5Zw7mwrq+urrOvtr20t7u2w8m/yc2/7ONRNjY4MDExLzg3MjM7Q0E9P1BK6crCvb7Au7a8usS3v8HFuru3vMfLzkNTRUdUQz4+P0RPOEU6PkJAS1FhSl1jcdno3cbPy77Tvb/OwevS09LtdWlbUe5mWlVRR1k8QUxSWkxTT/ZJZl5QW1Fm7PjFXszUzOrham/M9Wno7uBZXWvleFdYS1ZZTv5cW1j8XF1O71/l6ePld9rX58Zh6mnd39pS0eH40W76695ueFj74+nuWW/nands5Ezw6WnrVGtba01nUmFObPJ0UV5e/e9KUV/c++JbXNrqX+3g6PPt5tjf3uLe0Wrr0FzVd9xQ1uNe9WJs4fxZ6ln4aFxd61pdYuRi00/Obmz2W9lV4Pro83bm8+HW1HfZadfeft7r3Hlb6Npt+Grm2ebt+9zl9+ph5/5j3nLSXGNn1318XXL9/O1T7Otl7VrWVeBh83dz8GDpfN9n3lzV6O3t8d7e4WzT/dtj03XY7vva9eBd03Ld6mfw9uZwXtZd9mV1/HDrXuBk2lXkaOb4bOpd3V76e/j7cHv86PR+6f7z6nhw6+37dXVr5ml292rjc2bsb/F9dvzubvx1ePd271/lXetkdvpg8GZ5/Wz3aOt0+X598P75fP5673l7+Ht+8X357/n17/Ls7Pj17/bz9378+nX4bv11e3Z0cXd1enV7dXd6dH54d3d8dXt2cHd0cndyfXZ4fnP5e/t88370+vz7+P39+X77+n73+vf3+vL58Pj09fHw9fPx9vP5+Pn5/fz4efR6+X76/v7+fvz8/fn7+Pv5+/v8/f9+/Xt+ent+fXx8fPz9+/n2+Pn3/Pb/+nj8enp+dv18fXl9ev3/ffn++Pv3+v78dvx5e3h6/fr2/v5+fv1++/n79Pfw9fH39Pf7enV3/f97fH16ev3+/PP17vTu6uTm6ujr7n33eH14cfDv+vjy7Ph9+/x9eP5wffv9d/H5fmB0aWPmbvLq2fTmbunc6tn9b2dUb1Ne9E5j91zT4+/hxOzmamx+3NzTU8pk3NvZfDBuMb/b1WxsvTdJOvxFUdNDbl/Mv0y719tSRE87ZT7HRLt01NbLzsvt2M1Aw1zNTvT56mNV203gW/3O3dLO5MR83XFb1XjsceD26H1yzU/ESOBkblZNTmNmSu5Q2XJk1vpl1lTM7GFhYNv42d3CydPZbcbwZUZoWn5XSN5J3UBCQT9FN0E9UFJVcNq8vK60rK6sr7m3yMPZdXV+WEtATUQ8NDA3MC0eGxseICQpP7ispKGhnqGnsLi7wvFIUci3tbi3r7O7zkVLOy8tKS8xIxQZIyrLO+uinpieqauqrsZvNkvmbr/Kr6yoqraz4uhLNjM1LSYwKC0xFxolMMvI2K6goJ6qxcTb6+xZP93Et6isqK+ttN/WST42Mis1Piw3MC0uJR4fMPu7sbitp6Wmsc9PUubf1OlZxbCqqLC1ubzCX1VHOzYwMC4xNDAvLissLS5B793PyL+1sbO4vcHBwcPFzNrKu7Wytbe4urvCzOJbTj46ODQwLi4uLSsqKysvOD1BTfPFu7SwsK+wr7C0tbi7ubW0trm6u73Czt5eSDszLywrKSYlJSYnKiwuNkj8z8S6s66qqqutr6+wr7Cxtbezs7a3u8LTc1BCOTAsKCYmJCMjJCYoKy40PljMvrq3sq6sqquus7WysK6vsbKzsK6wtbzG3mdKOTEtKigmJSQkJScoKSsvN0Rny728ubKtq6qssbWzsbKwra6xs6+ur7O6xNZfRTkxLSklIyQlJSYmJykuNDlATdi+uLSwr66rqaqrrrK0tbCtrbG0s7a3ub7N+Ek5MSwqJyMiJCUlJygpLTY7Pk510sO3s7S2tK6trbC0trWzr6qpq62vsLCvtr7PWT0xLCknJiIhISMmKCsuNTpEV/zWw7q0sbW2trCur7a7t7WwrKmpq66ysLC1u8xbPTIsKSclJCIhISMmLDAzNj5KXd7OycG7uLi7vLq3tLO2t7Ouq6ekp6qtr7Czusl5QTYtKScnJiUlJCQmKi43Oz9JWujQycW9urq6u7y7t7Wyr66sqqempaerr7O4vsxcPzMsKSYlJCQjIyUmKS00PEVNXPHaycLAv76+vb6/vbu4tLCurKmoqKioq6+0vMfmUTswLCglIyQjIyQmKSsvNj9IVWt55tDKyMfIx8S+vr27uLCsqaejoqWoqaqyv89VRjsyKigoJyYmKSkpLDI8QE5n3M/b+FRPSUdIT17308q7s62ppZ+dnZ+jrsRYSUpGaFhqVUQ9NTAsKygpKCotNlDv0eH8VEI8MzAvNT1T5NvMysK7tK6rp6Kfn6Cnr89AP0PSvLy9dkY3MDAvMC4tLC40Plny2GFHODIzMjY2OT5IZXbu393Kv7WtqKWfnZyeqbFPOjlJtq+rt2Q5LS4wODY0LisvM0RbePNKOjAtLzE0Njg6PkJCR1LkwLivsa6ro5ibm6nA4TJES72rqqrBTjEuLzU6ODcyNzlBR0lKPjs1NTQyMS8yNTY2NDI3PvS9tbG7ua2imJmbp8VkN03Qs6mqrbzdTEA9PTs3NDU9SmJXSzw1NTQ5NjIvLS8xNTMvLS0vQ9O6sL64vaudm5ihrc1FSle7raipscDcTkREQ0k7OzxAWFb4UEA5MjYzMzEvLy80MS4rKSstP966s769wq+inJifqMNQRkbCtKurs7nJ4GRKTUY8OztHWnTuUkE5Njk4NTAtLi4zNC8tKSksNGHEtbvEzL6qoJmcn67VYTxrybKrrrDAzOJkZ1FFPTxCUGJrVkc/Ozo5NzMvLi8wNTMwKygpLD1qxr3DwL6vqKGdnZ6swU89WduwrKuuvsxRVEtJSEBFRVJcWk5CPDo6Ojk1MS0sLTA3NjMtKisxRta7tbW1sammoJ+dn6y9RT9H3rKtqbG+80JAPEVERkVES0pLRD87Ojs7OzcyLSssLzU4NzEtLTFD3LmvraysqKajoJ+eprLoQD9Hx7iur7jITUA3ODk8QEFMSk5IQD05PDs9OjUwLCwsMDQ4OTU0NDtQ0LmxrquqpaShn56fp7N3QjpA3sCys7bEd0Y5NjI2OT5IS09IRT88PDs8ODUwLi4tLzI2Nzc4Oj9Q2763sK6qpqShoJ6eoqzCaD4/TeTDv7zDz3ZLPjYzMDI2Oj9BRUVDQkFBPTo2NDIxMDEzNTc5PEBMd8q+uLWwrKqnpqGenqCqttpSSUlPVGpsffxwX0tEPDo4ODo7PTw9PUBEQkM/PTs4NzU2Nzc4ODo9RFXpy8C8ubSvrq6rp6OipKiutbu/xs/cZlJIREE/QD8/Pj0+Pj08Ojo7PD0+Pz8+PT4/Pz9APjw7PDw+RU9f/tbIvri2tK+ppaOjpairq6yus7rF5lZFPz08Ozg1NTY3ODo7Oz0/QUFER0hEQUFBQUE/Ozg3Nzg6PT9FTV3fysC+ua+qpaOjpaanp6epq7G8y/RVRkA+PDczMTI1Nzo7Oz0/QENITk5LR0RDQ0E/PTk3NTQ1Nzk8PkNPet3Syrqvqqenp6inpqWmqKy0vMbWelxTS0E6Nzc6PT4+PT0+QUVJTExIRURERENBPTo3NjU1NjU2Nzo+R1VdWnHIuLCsq6urqKakpKaprrK4vsfQ4mRMPzs7PT4+Pjw7PD9FS0xMSklKTE5PSkI+PDs5NzUzMTI0Njo/QkJFUtnAuLOwr62ppaSlp6qtr7K3vMXW+2lbU09LRkdKSUVCREpOTlBPTkxNUldSSEA+Pj47NjEvMDAwMjY4OTs+TXbPwbq1sa2ppqamqKqrrbG4vcPO1tvtZltTTk1NTEZAQUhMT1BOTEpMVVpRSUA+QkM9NzIwLy4uLzI1Nzc5Pkdq0cK8tq+rp6enqaurrrG4v8fJw8HDytjl+mlWTEM9PUFMXWltXmFq4ODg/E9EP0VEPjQwNTcxKCYpMjs8Ojk/Teb358i3qqalpqisrbS9xMrNycC8t77AxMnQbUw/Q0VNTFFcX2xZddrZbEpEY9v5WkZNUEA1MDE1NywrLTI6Njk8QkFUTkr5ya2pq6yrqquxw8bGw8HKyb23usj5aedvU05mztxeWvTV3WRrzL6+4GF7ZVZKTvXXTj05ODovKSsvNDMsLTc6NTM4UW5Cbrusq7K0rKqzw9nEvs9u2sC5usTCw83tUHLPyNDSxb7I8e7b0djNxr/A111fblhWUVhZSDs2MC8vLS0uMTQxMDU2NTs+VVlNx7OusbCyrLG7vsfCx8/Wxr64ur7GzdtuWFje1tzi287Obk5z1M/Z18S+zWxd8G1JSl91TT07PjUvLy8zMTEyMTM4Nzs/Pz9BYLy6vLOuq661tba/ydXXy87Fvbq8v8TJ/VFl7Nfm6s/N2mxdbepkfdDFyc3Q0dVtWVVPT1NQTUI/PjgyMjMxLy81NzQ4PkM+PT5WzczBtK2tsrWwsLvBw8TM2M/Iy8m+vb7K0tbvXldd8/zx3uDdbFNba2h18N3Q1ub3aFhKQUNGR0hERkc/Ojk5OTY0Nzo8OTpBTE1JS9bCvrixr7C3tra9v7y5uL7Cu8HO2c7I0OHU0d1yWWtrU0lITE5HSFJcXllXZmlUT09XW09Scd7V1eTsX0o9Oz06ODY5Pz09QkVMS0dX8eHVyr+6vr25t7i5ubW5v8LBw8jSzczM0M/Mz+ZlXFJKQEVISUxWWWJfWU5KSktJSU9SUUxNVn315NnS0+RlXVZMRT9CRURFTVtlXF9vcmpu5dbR0MnFwsPCv7/CxcTDxMfHxcbLz9DR2ePu7vloXVtcVlBMTEpHQ0NEQ0FCREVHSUlJSktLTFRkfenZz8zMzc3Q2epuYFpUUE9QUlJQUldcX2R16t/a087LycfFxMLAv7++vr6/wcTHy9DX3+52Z15bWVZSTkxKRkNAPz49PD09P0NITFRgb/rl2tTS0M7Nzs7Pz87Q1dja3uTo5+nr6/D9fHlwa2pqZ2VmaGt29+3n4d3b2tfT0dDQ0dPV19rc3uPr+nNqYltVUE1KSEdISUtOUVZbX2RlZmRgXVtaWltdXl9hZWdqb3r47eXd19LOy8rIyMjIyMjIyMnJycrLzc/V3exwX1ZPTElHRkVFRkZISkxOU1hfa3zu5N7a19XS0NDQ0tTX2t/m7v1xa2lpaWlpaWdkYmFgYGBhZGhue/Lp4t7d3Nvb29vd3uLp7/h9dnBubm9yeP3z7evr6+vt7/L09/x8eHNwbWpnZWNiYWFiZGZnaGprbG5vcnR2eHt+/v38+/r49vXz8vLy8/T19/n8/v9+fv78+/r49vTz8vHy9Pf5/P58eXZzcG5tbW1tbG1tbWxsbG1ub3Bydnl8fvz7+/z9/f79+vf18vDv7+7u8PH0+Px9eXVzcXBvbm5ubm9wc3V2eHl7fv369vPx8PDw8PDx8/X3+fr6+vn4+Pj39/f29fX19vf4+fn6+fj4+fv8/P39/f38/Pv7+/v8/n59fX79+/n39/f4+Pf29PPz8/b4+/59e3x8fX5+fX17eHd2dnZ2dXV1c3Fvb29wcHJyc3NzcnNzdXZ2d3h4eXt8fX5+fn59fX19fHx7e3t7enp6enl4d3d3eHh6ent8ff/+/f79/fz8+/z9/v7/fv/+/fz69/f29vb19vf39vX18/Lw8PDv7u7w8vHw8fDw8O/u7e3t7e3u7/Hy9ff4+vf39/Hv8fP5/v38eHl8/PP6/vz8+317eHj78+7scPjt+eTd6uLZ2n1mcexeXWZfWEFORm5bsrhMyV/GS0tGQjdDSfX+4tPC089nzl5xSeBz3c7KycfnyN7bX9xf1XTscObt3H7W3Gr6W2pqWVRbXUpZSnFUY1DuWFdjTlhNTkhqTV5SW2FkYVt3XFxaV/9dXl98dl3qXNRS9Hfu5nnZb81ZzF3MW9F81+nX6N7SbM9u0WfQXM773+fY/Orfa89d02jNa9Dq3Nju6O3bbe/r+uXv/9/34nnebNpc7mzwYvl69etva/l4ZWfuZnRjaHP5X2fvXHlXZm5pWXD3W3tpbm9wZHx7X+1V7mV2cHZ7dt5c4mR272tmaORa5lnx9e9w7t9q42ftcfFqbvjt/2Tu5e7zfvzr8mBz+25uYP3r9W/w7vzsYnt7c2r9b+zufO7p4Ojr8uT4dXt85Pju99/y9Pr66f7u73z7fPXyb3rx6Xz67+Xf/O/r5P5ucXvxafzz6+/s6e7qePB7/XNsdnp+bPPw8vn78XB5bHZrbXZ0+3t9fX76ePp6d25ycGpscntwcnbydXt47v58cn1+bGxrfGxsaHZ0bH14+Xl9dXNpavxwbGtzeXNtd/D9fHB6eXRqdP79fHZ8+Pl8//74dnJwdHp8/H71+PD39ff2+fv9/PX9/nz3+/x6fX59e338/vj07vDx8/T3/X39/P3++ff19fbw9Pf7/fx8eX718/Tz8O7v8/P3+/57eHh3eX3++vn9/v7/fXl3eXx+/vz59vTy8vT29/X09PTz8O/v8PDw7/Dy9PTy8vT39/X1+fv7+fn6/Pz7+vv7/Pz9fnx7e3p5eHl6e3t7ent6eXd1dXRzc3N0dHRzc3R0c3FwcHFxcXFzdnZ1c3R1dXNycnNycXFyc3NycHBwcG9vcHBwcHBxcnN0dXZ3d3d3d3d3d3d4eXp8fv79/f3+/n59fX5+/vz6+ff29vX29vf39/f29vTz8/Pz8/Pz9PX19fX09PPy8fDw8fLy8/T29/f39/j49/b29vf4+fv9/359fn5+/v37+vr6+vz9/318fHx8ff/9+/r4+Pj5+fr7/P3+/v39/Pz7+/r6+vv7+/z8/Pz8/f3+/n59fHx7e3t7e3t7enp6eXl4eHh4eHh4eHl5eXl5eXl6ent7fHx8fHx8fHx8fHx8e3x8fH19fn7////+/v39/v7+/v//fn5+fn5+fn5+fn5+fv////7+/f39/f39/f7+/35+fn5+fn7+/fz8+/r6+vr6+vv7/Pz8/Pz8/Pz8/Pz8/Pz8/f39/f7+/v7+/v39/fz8/Pz9/f39/v7////+//7+/f39/f39/f39/v7+/v79/fz8/P39/f7/fn5+fn5+fn5+fn5+fn59fXx8fHt7enp6enp6enl6enp6ent7e3x8fHt7e3p6enp6ent7e3x8fX19fX18fHx7e3t7fH19fn7///////9+/////v7+/fz8+/v7+/v7/Pz8/f39/Pz8/Pv7+/z8/f39/v7+/v7+/v7+/v7+/v7+/v79/f39/fz8/Pz8/Pz8/fz8/Pz8/Pz8+/v8/Pz9/f7/fn5+fX19fX5+fv//fn5+fn19fX19fX19fX5+fn5+fX19fXx9fX19fX19fX5+fn5+fn5+fX19fHx8fHx8fHx8fHx9fX19fXx8fHx8fHx8fH19fX5+fv///v7+/v39/f39/f39/f39/f39/Pz8/Pz8/Pz8/Pz8/Pz9/f3+/v7/fn5+fX19fHx8fHx8fHt8fHt8fHt7e3t7e3t7e3t7e3t7fHx8fH19fX5+fn5+fn5+fn5+fv////7//v7+/v79/f39/f39/f39/fz9/f39/P38/Pz8/Pz8/Pz8/Pz8+/z8/Pz8/Pz8/Pz9/Pz9/P38/f39/f3+/v7+////fn5+fn5+fn5+fX19fX19fX18fH18fHx8fXx8fH19fX19fX19fX19fX19fX19fX19fX1+fn5+fn5+fn5+fn5+fn5+fn5+fv////////7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+/v7+//7+/////////////37/fv//fn7///9+fn5+fn5+fn5+/35+/37/fn5+fn5+fv9+fn5+/37///////////////////////////9+//9+//9+/35+fn5+fn5+//9+fn7/fn5+fn5+fv///////37//////////////////////////37/fn5+//9+fn5+fn7//35+fn5+fn5+fn5+fn5+fn7/fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fv9+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn7//////////////////v7+/v7+/v/+/v7///7//////v7/////fv9+/////37////+/v/+//7+/v7//////v7///////////7+/v7+/v7+/v7+/v7+/v7+/v7/fn5+fn1+fn5+fn5+fn5+fn5+fn5+fn19fX19fX19fX18fHx9fX1+fn5+/35+/v39/n19//v8fvz4/fz5", "UklGRnROAABXQVZFZm10IBIAAAAHAAEAQB8AAEAfAAABAAgAAABmYWN0BAAAACBOAABMSVNUGgAAAElORk9JU0ZUDQAAAExhdmY2MS43LjEwMAAAZGF0YSBOAAD+/v79/v7+//7///9+fn5+fn5+fn19fX19fX19fH18fHx8fHx9fX19fX19fX19fn59fX5+fv9+fn5+fn5+fn5+/35+/37+/v79/fz9/P39/P38/Pz9/Pz8/P39/f39/f79/f79/v7+///+/v7+/v79/f39/P3+/f79/v7+/v7+/35+fXx9fXx8enl4eHh3dnVzdHV2c3JwcG9vbmxra2pqamlqamtra2tpaWpra2xrbHN0dXd3dn17fvj07+/59vD06vDy5uLf3dnX1NLOz9DV19bb2vrrbV9OW1TRucvL1c3S1PxsSkpQTGduZu3bycjIxM7Bwbq9u7++wMTH6d1mfF99eGdsX31hYUtOQkI/Pj06Ojo6OT46PDc6Ojs5ODg2ODk8PT49QENGSElTUV1YYFlgZ3V23vLn3dvT0M3V5NvOyMbJzc3OyszLz8/OysrNx8nKzMrGw8bJzc3Q1c7LxsnL0tre1s7W0t/q2tnZ3+/o9fTt3dPW1+Xp4dbd9u5ja3jt3d/n3d7e29vg7O/9ffbj33Rq8OH75NXd5+Ls7H3zaF997/dpXVpn9nddWF1lbPteVk9WY2FeZ2hodGVq/O71alZYW1taXV5VUVRn/vrub1FPVHzv/lpVU05c/e/+VlBVZe1wYltfanfl9mVfaPv8eG9hZHF0bnV1b/rq5ujh8urf5Ojr/nd82tDa8O7c0c7fbnJ58erle2Bs9uXb629w7uHj8nVqaHfd1/JeXGPu4vNpX2D12NDX73D23tTY6GVea+fY32pcYfDh5u5oXF9ja11TVVde7t7d4+fc0MvM0trd1M3N09rVzcnIzdbc3trja1pcZ15MQD09QUI5MzM5Qkc/QFPOuLGvrauop6mrq6usrrO3uLq+x9DdcUw8MiwmIR4cGRUWHCIrMDrar6inqquprLjF7VtdYdq9tK2ppaGjqqyut81OPjkzLSkkIyIfHBodKiwpL0TFsLSyq6iqrri6vNxUYt3Hv7uyq6qrra6utb/GzGxJRT83LSclIx4bHSkrJjF0w7ayrKOkrK2tsr/2W9nY/865sa+vrKqssLS4vdZXT0Y5LiopJSAdHSItJihc0d21q6ilq6ulrr/A0GFVTe3N4L+wtLKurq60u7m/blRMOi4rKSMgHh4lKiErWkBFu7Kxr7CnqbevrcLCu8vFwMm+u7+7uLu+vsjP7FRGOi8sKCMhIR0gLSsnY8tet6itqqepq660ssLvxs5O5dJm9+B5dWhd6etS9M9kYtNmSk8/NTIvMjUsM09AQ8S+yrm0vsHD0l1JQVJKPmrK38GwtLCrrbKwsr/R1Fw+PDcuLSslKCwkJi4sKjl4TGC0sr6rpa+tpq62rrPFwL7W3M/d4/FdXEs9OzMsKyYnLSQmPDow0L3ZsqmyrKatr6+4u7/h191MS088OzsyMzEuRD8y3sZKuqzGtKm/vrHYYMdKPnBDOnVOQGFQPkQ8NTcsNUowPL9d36mvtqKnsKWnubG119nkRkRDODQxLi4pJywkJD4xLM7GTq6msqafqqmhrLWvvd/TYUVKRDo5NzMvKywpIS04JzTJSWmprLKgoKujoa+0r8dq4Ug8PzkxMjAtKioqIiY7LircyEywo66lnaWln6mwr8DU10NATjcwOjErLConJCIxLydExErCpKuqnqCloKWtrrzPz0Y6SDouNDMsLCspKCEtPCkzvfvxqKasoJ+ko6Wsrr7a0kw1Pz0uLzQuKyssKSMqPC4t3Mj0s6anpKGgn6WqqbTQzPc8PT41MjEwLywqKyYkMTUtO9vRwK+ppKSloqGpr6+722FKPjw2MzQxLzAuLCwoKzUyMkf00L2zqqWnpqKkq7C1wG5JQDg1MjAyMi8vLzAuKi85NTZJZNm/ua6oqaumpquwt7u+8URJRTcyNjg0MDE0MS4wOTo6Qm7WzsCzrq+vrayutLW4vs7X7mdORkJEPTg4Ozk0MTU4Nzc6RExXbdXCvb25s7K3uLa4wMTJy8/p9OV4X1ZQTEtFP0A+Pjs/QUNFUVpk9trTy8fCv769vr7DwsDJys/O2ebg5mtsb2xRWFdPT01PVlNaVWN+92jv3+Pj3tza1OXg1djf8+/f5vV07+PmYHPn5mxv8e3p7mzu9O16eHzs8mb85nPrXf5sZfhkY19mXFxiZnZoaXR2an3+9HRubGv4+/14a/d493nseeVk8eXte+x9cGxt62pudXpneer2bOxtenzj7exr6959fePqfW7w+PXg63todvFx+W1kfnRqaf1dfvRlaubgePF57d1qbNfhZmTm3tz9dvPicWXw8OhcbnzfYe5kc/xjbX5u5/Zw+OXhfmNuX2pfdP3v7/9r4eBw93VX8Plu6ORzY2/TYOjq4O1aW+l+bHnt6vVuWmLg/mrd1t9s7tPW3tz3W+Zwfuly4eXwbnzPaGxj/FxNYeP4YWlW0GxHWO5rTUl32HdgTXnNfExR3tVQVu9jaVpm7NjSY05j105HVdHTY97M1tfvWk9fZlZOXGTefG5e2tfWXFFizfpRSebd2OtU/sPP4NDd5MzqX2nQydhn1dPMzM7a5ufd3GlPZs/Ta/ze1eZreeri+GDv8lFeaeHQ3Pr4TlfnZGxURn7V6mNEUOJyTUpT2dXsX1v71vt8UExWa33sWmXl09bebn7r2uPu8dTe+2l+aeDKzujle+fl62Bba97Q4lRMZNHLdExKWeHP92BXcNDYa19eddnneWtu58/V2PxZTFZZWl5f5tfX5Vo/TFnZ0fBHSkhT4NvaaUpOXF39VXDnzNTvYFZg69DS3WRgYu92a1Nmd2f1aF9m8FlnU09aV2Li28bF287Oxr3D0MzQwbu/xN5g6Ovf6UhBPz9CPjg2OD9OU0w8PERbzsC9trCuqqqtr7e9ur68w9dlS0NFRDwzLCkqLTA0MSwrKi06Ur6vqqelp6eprKyurq6yub/ZXkxKTk1ANy8rKyssKyolJSgqPM6roJyfprK+vbeur7S8x8e/w9NPOzg6P0A4LyopLCwrJR8hJze/p56co66+1si6tLO5xsvHx8fmQzs6P09LPC8qKi4wLyYgICc5vqCal5ypwWFmybu2vc7a18DAzFg7NTpBTkw5NC4wODIsIyElMHekmZWWobVPPE3RvLjI0+vOv77QTjk0OjxJQDk2Mjc2LikjIik0xZ+Ykpekvjw0P3vCvs/b/drGyNhZPTg5OEJAQD88PDgwKSciKy/Nn5iRlqG9Oi44TMS8wMrZ3svGyuxNOzg3O0RGTEpCOjIoKCIpLUennZOUnKtpLzA2c8PExdrh1cPAw9xNOTQwNj9HXE9ANysnJiMrLcelmpOYn7RDMC847dDBz+ny2sO+vsxROS8sMjlY6/5ONiomIiIqLr2nmpWZn7VLMi83X/PG1+nV0b27vMRkOzErMDhO2vFcOiwnJCIqLM+pnZWana9uNzI2UnXP02nV0723t7zVQTIrLDZD19bq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c9LvNCYmMVnOQCogHS8ub6ugl6Gwazrnr6Slul5Tv6epuD4qMEu7uOY5LjRmy/05JyYvS9tKKyEeJzsyvaibl6i9Pj27p6Os7UnYrKGuWyoqSL2z1zYtNGS/6TkmJDBM0ksqHyAmQTU+rKKVnbZeNMyooqrXQN2toKpjLShMu7TVMis2W7zMNykkME5kQyshISczOy63oJaXs1U24aifq89BdKuiqGYrKkm6t/swLDrlwd4yKCg0Uk87LCMjKC05LduemJWsTT/Np5+u/kP7qKKo+ywtULu5UC4vPs3FXDIoKzpLRzUrJSMpKzUuTp+alqhNU8enoK5ySvqqoqvUMTJhw8VHLzJH189IMCstPUI+NSspJCkpLzFBn5qYo17cu6mjsmdVw6uirvU3MlvLzkQxM0bj7UUuKy05RDw2LSonJiovNkihmpqf08W3q6W3YUvAqaOr6z08Ts1yPDMySt/fTC8sLzZAPjUuKiUlJSswN6ianJ6yuKuop7FlStG0qazNZ2vvvtBFOS84TElDNS84O0BTSj41Li4uNDMzYr+5tLayrKqnqK2zt7WztsLl8+fb31xKQDw/Pzw3MjM1NTo9OzcxMDAyMjA3S2LfxbmvqqWioaOmqaqts8XtX1dXTT86Njc5ODc0MDA0OTw6NTEwMjQ1MztZ2ce6s6umoqCjp6qtrrG831ROU1lLPTg2ODs6OTMvMjc7PTkzMC8yNDU1PXvGu7OuqaOhoaWqrbGzt8ZkRkJITUc8NjM0Njc2Mi8xNzw/PTg0MjQ4Oj1J2MG6saynoqChpaqusrW5yGtIQkVIRT85NTM0Nzg1MjE0ODw9OjQxMjY8RFzJu7ewrKeioKGkqq+3vL7LdEs/PDw8Pjw4MzEyNTc3Njg6PD4+OjY0NztCX8W2s7CtqaSgoKKor7nEydF9UkU9Ojg7PTs5NTIzMzQ1Nzs9PT06NzY4PUZlwLCtrKuppqOhoaatucbY7WNQTEQ+PDs8Pj48OTY0MTAyNDg6ODYzMTM4Pkrdt62sq6uppqSho6mxv8/oaVpVU0pEQT8+Pj49OTQxLy8vMTY5ODY0NTk+TuK8rqurq6upp6Wkp624yd1wXlRNT0tGRUA/Pjw7NjIvLi4vMDM1NDMzNzxIbcu3rKmpqqqqqKamqa+7zedwZFZRVlJQT0tIQT05My8tLCssLjI0NTU2OkBV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Ix0cIickP666y7Sz1DQlJy0lJDfdvL7SyL3PVUNBWc+8raKjqKmorLO2r62xt7SywV5BPTIkHB0iJSE5ucDCrq7LRSsoKyUgKz1q0NzStbDKTEt4ybu1qaOmqaamqKyurq2zvb3IaT83LykfHR8nJCvyvb62sMLeQCsnKSUkKjdO39HFsa253+O9rq20sammqKmpqKits7a5wtlRPjgwKiQhICMnKjJL1si+vcTUZkE3MzAvMjlCWdrGvLe1t7i2sbCytLSzsrGzt7m8v8PJ0+D/XlNMRT87ODY0MzIyNDU2OTw+Q0dLT1VaXmVufu/l3trX1NLQz8/OzMvJx8bEwsHAwMHDxcfJzM/W3el7ZFpRTEhEQD8+PTw8PDw9Pj9CRUhMUFhfbPzq4NvY1dPR0NDPz8/Q0NDR09TW2Nrb3d/h5Ofp7O7y+P55cWxpZmNhX19eXl5fX2BhYmRmaGpsbnF1eHt9//79/Pz8+/v6+fn5+fn6+/z+/318e3p5eHd1dHNycXBwcHBwcHFydHZ4enz+/Pr39fPx8O/u7u7t7e3s7Ozr6+vr6+zs7Ozt7e3u7u/w8fP19vj6+/3+fn18e3p6eXl5eXl5eXp7e3x8fX1+fn5+fn59fn19fXx9fHx7e3t6enp6eXl5eXh4eHh4eHh3eHd3eHd4eHl5eXp6ent6ent7fHx8fHx8fHx8fH19fn7//v78+/v5+Pf29fX08/Py8vHy8fHy8vLz8/T09fX39/j4+fr6+/v8/f39/f7/fn59fXx8e3t7enp5eXl5enp6ent8fHx+fv/+/f38/Pv7+vv7+/v7+/v7/P39/f7+/v7//n7/fX59fX18fX1+fn19fX1+fn7/fv7//f79/f39/fz8/Pv8/Pv7+/v7+vr6+vr6+fn4+fn5+vj5+vn59/j4+fn6+fn6+vr6/P3+/f79/n5+fX1+fX1+fX17e3x9fv3+/v37+fj39/f29vf19Pb19fj29PPw8fT19Pb3+Pb4/n15/PHw7/P08O7s6e7y7ezr7O70+u3o9ntweXp6eP32al5kbWpoaV5nbFtcY/jfcW5aTV5PNFn4RENn2/TRw9Xuyry68D4yMjtER0pOUVJb7N16VUxJQ0RDQ0lLSk5UW15bWl/p2OFw9t3YzMTCwb+9uri1sbCwsrS2trSysrO0s7S2tre4ubu8vLq7vsPIyMXI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feLZ0s3KxsPBwMDAwcPFx8rMzc/Q0tTW2t3j7f1vZl9cWFZTUE5NS0lHRkVFRkdJTE9TWV9qfe3j3dnW1NLR0dHS09TV19jb3d/i5unt8fb6/nx3cm9samlnZmVlZWVlZWVmaGpscHd++PHt6ujm5OPh4N/e3t3d3d3e3t/g4uPl5ufo6evs7e/w8/X4+/3/fnx7enl5eHd3d3d3eHl6e33//vz7+vn4+Pj3+Pj5+vv8/f5+fXt6eXh4d3d3d3d4eXl6e3x9//38+/r49/b19fTz8/Ly8vHx8fLy8vPz9PT19fX19vb29vf29vb29fX09PPy8vLy8vLy8fHx8fHx8vLy8/Lz8/Pz8/T09fX29vf39/j5+vr7/Pz9/v//fn59fX19fX5+fn7+/v38/Pv7+vr5+Pf39/f29vb29vf39/j4+fr6+/z8/f7/fn19fHx7e3t6e3t7e3t7fHx9fX1+fv/+/f38/Pv6+vn5+fn5+Pj4+Pn5+fn6+vr7+/v8/P39/v39/f7+/v7///////////////7+/v7+/v7+/v7+/v/+/37/fv///35+fn18fHx8fHx8fHt7enp5eXl6enp6eXl5eXl5eXp6e3t7enp6ent7fHx9fXx8e3t7ent7e3p5d3d2dXZ1dXRzcnFwcG9vb25ubm9vbm5vcHBxc3Rzc3N0dHV2dnd3d3h5ent6enl5eHd2d3Z0dXZ4ent9fX7+/v7+fv5+fn7+/X59fnxzcG1ucG9ybHhnYWf2fMTJWdzz1V5ieedbeWXr19/n2NDU2NjP1tPU0NLR1dbc5ez17+77//x2bmxiZV9bWllWV1VVVVNUU1JUVFJTU1RTU1RVVVZVVlhZWVpbXF5fYGNlZ2dpbG1wdHN3fP9+/Pf39vb29PLw8fDu7Orq6eno5+bl5eXm5ubl5eXl5uXj4+Lh4eHh4eDg4uLh4uLj4+Pl5+fn5+jr6+rq7O3v7/Dy8vPz9/j49/j8/v7+/f5+fXx8ff3+/v38+fn5+fj39/b09PTz8/Dw7/Dx8O/u7e7v7u7v8PDw8O/v8PDz8fDw8vLw8vPz9fT19fT3+f38+Pv9/Pr8fHt9fXx9+/r9/fr5//33+f96ffv6/X14d318+vr79vt9cm53enFtc350b3Nzb21zdXh1fP569/t7d3X9+Hloa21jaWZrdWZfYGtoZmhnbWlfYmNgZWtyc29ral5fbGpgWmxpYnZ87XRr08zbe2lqV15bU2lsYGDlytDm5uxoXGLs0tHg6vno63nw6efW1eTb3ndgWVdXUVJbXV1bbtzb3eft6GlcZV9fXFlv49/SysPAyc/L1f1VTFNYX2pZWlFBOzg2NzY2Oz9CR0lKS0g/PD1CTFz2zr2yq6ahn5+kp6OhsFEwLC8uKzjnvbzYTzsqIR8hLDxZyLi2vNk/NTMrKzM8Z9nhwsLMx86+rqyoo6KgoaeopqOsPigtO1Vk57Kru2I9MiwjHyc5X9TQwLrPQzYxMi8uOk9XWFpUSj85P2nOuqukoqOnqq61sq+qqP8tMj1NSD7Jr79NNC8vJSAoOfDP5c3JUTYtLjIwNEvi9VNFRUE2NT5Zw7avpqKmqautsLewqaapfiotPD5KVsKzyUQ4LywnJC5H6M3dcFc3LS8yOz1AWvtaRzs/SD49S+3At7KrqKmsrq+ysKypo6OwRCUsRkdO072zzTIvLyspKjTfzfF4XD8xKi48QD5Hat1ZOjlLVkZETdK+vbmvq6qtr66ura2spaGn1iUiPEFC28eyuUI0NjAtKjBtyNDaXD8zKSk0PkZOWupmPTQ+V1pabHvW19a/r6upqqysrrCvraiipMEpJDM+SV7GsLdIMzQ2MC017L/LY0s/MykoMkFDQ0dQXjwxO09XaXLe0mFoybuuqqyrrK6trK+sqKKi4iUnLzlHUMCsvEQ5ODQtKTXaw8reZ083KCcuNjg3PV18Pzg/S1BNXs/Pd+LHt6yqqqqrrK2vraysoaPbLiorMDtHwLLEUURAOC0rNU9u/tzcVTYqKS0vMDI7UEs+QUtOSkVpz9vi1cO2rq2sra6urqunra6io7w3KS4zLjnPtrz+U2JALSouPEhIXs7WQC4sLSwpKzVITktHW3pLRl3c297OvbKvq6mrrKquq6ezsqOisFgvMDEqLUjMws7ex9w8MC4yOTY8c91dQzcyLiglKi40PUhe6N7mYXF+ZnvWurKvqKiopaiqpa66qKSos11APi0nKzJGVFDOvMZjQj48MCwwO0RFSFVVRjg0NDEtMz0/UWrOvL7AwL67u7++ubaysbOysbOys7W0t73CzNz6V0xLR0NFQ0FCQUA/P0BBQkVISkpLTExNTk9UU1RaXWBjYWp+8+3i4tzY29fV1dPRz83LycfFxMLCxMTGyMrNz9Xc63RkVk5KR0RCQD9BQkJDREZISUpNTk5RVVldX2JseP/z6t/b2tfTz87Ozc3NzMvMysvLzM7Q0dne4uXs/Htxb2leW1taVlVVVlpaWVpbXFlWV1lYV1lZV1xeXl9r/vHr4NjV09PPzc/PzcvLy8zLzNDR09zn8HHv9lxmXE9UT0xNTUxOUlhXU1JTTUpLQkVJSlRcceNtXexJXUrRtN3R6MXtvMFSv2nb1MS53HPKy93s1M7gSEhJRUQ9TlFWTF5tSk5DSkdAP0Q/PkFGVVroysK8t7CvrrCzsrzCy8TT0sbOycPBv7/HxcfkalpNRjw4PTczODc3NTQ0NTMvMTQyNTtDUd/Esqqqp6enq7C5vsxoWlls5enJvLy8urq/ze1hU0Q9PT5DPz1CRj87OTUyLSoqKikoKy4wNz5N3smwpammoqSorri6yFhb7frczL20ubmwtb/N429PPj9JQEFPTktHPz46MC0tKicoKissLjY7P079z7qnpaahoKSpsLa97lBncWnhxbq4ubWyusXQ5l9DP0lAPkhKSEM9PzkxLywqKScpLCwuNDpAS1zOwK6jp6Sfoqiss7bDW/vP7+XGu7m7u7W6ztbeW0hASEo+QEtIPzw7ODEvLiwrKSotLS4xNztBTP/Ku6imqKKipquvtrnQbtDR2s3Au7u9vLrE2Nr4WE1JTE1DRUhBPTk3NC8uLSwrKistLi80NjpESvPRvKmpqKSkpquwsrXF0sTHzMrEvb3Ewr7I3OPza1pMUVRHRUM+OzY0MjEvLi4tLCwtLzEzNTs/SHngva6tq6enqKmtra63vL2/w8jKyMXMz83W3O9mYllOS0lIQz8+Ozg3NTMyMS8vLy4wMTI0Nzk9RVD3zL20r62rqampqqutsLW5vL/Dyc3Q1uDo5OX2amBcVU1IRkM+Ojc2NDEwMC8vLi4vLzEzNTg8Qk59y7uyrauop6anp6iqrbK3u77Ey9La4/f+9PZ3aV5YU01KRkE9OTUyMC8vLi4tLS0uLzEzNjk+SF3Ywbauq6mmpaWmpqirrrO4vMDHztvta15cXl1ZU09NS0hEQT06NjIwLy4uLS0tLS4vMjU3Oj1EU+zJu7CsqqelpKWlpqirr7W5vcTN3XxgVU9PUE9OS0pKSEVCPzw4NTIwLy4uLi4uLzAzNjg6PUFLYtnEubCsqaelpKWlpqirr7S5vsbP42tYTktKSUlIRkREQkA/PTo4NTIwLy8vLy8vMDE0Nzo8P0ZQbtXDubGtqqimpqWmp6irrrK3vMLL2fFhU01LSUhHRUNBPz49Ozk3NDIwLy8vLy8wMDI0Nzo9QEdQZt7JvrawraupqKenqKmrrbC1ur7DytLffmBYUU5MSkhGQ0A+PDo4NTMyMDAvMDAxMjM1Njk7PkNKVGrfzcO8uLSwrayrq6usra6wsrW4u7/Eyc7X431hVk5KRkNBPz08Ozo5OTo6Ojs7Ozw9Pj8/QkRFRklOV2b03tXOysbCv7y6uLi4uLi3tre4ury+w8bJzdPZ4fRpWFFRT0tGREJBQ0RDREVERklIR0hLTlBQUVJUV1ldZGVpePPu6ePg3trY2NbTz9DPzc3S0dLS1tvbdm9zc3B1/fPqfXRucG1xd21lYGNiZmttbGxrbHB5+u/p6uXg4OTj5eno6Ozu8PXw7/f28vx99+/19u3xffbw/X3/dGptdHN0fHltbW1paWlkY2loY2VlYGh0cHX08/Xt7u/k5e3j3uHn5enp6Orv6uv+fvr8ePt6d3V1cHr9fP769fn8fHt2eHVrZmVkYWlpaGxua3B5dn339/Xu9f329Pj37vp7/Pl++3Z0+Pn//n3++fr4+X7++O7xfPbw8vP2+vr4+vv+eXd6/3JwfHV4bnBybW54fXh3bHv6dW/+fXhtfvZ1/W1s/v51/fLw9O3wcHNzfHv69HpyefNya259+Pr07u/y8Hp3dP74d3xz+f9udH75/2xnbvBzbGz4dX72b3D09Xjm93n6/W946u5q/OH34vHY3frb4ez2Ymf+YF9dav5s9PPw4NR2XmBWaP7ofnb0alLd52NXS9/FXklNVVtXVUhYb/bU2uzWydLneVhgYPH6X13f0dzT3dTP0+DX0M3JfHXOy3LjxdXd2FPszdxXWdPHx9NfY+Pa9lBJUWNm2ehnWuDL33BvX/ffa1hb+25hWmBfV0xddFhNVWpUUuV4REtYZFlTTllPR1h1YltITF1s+U5JU3zk/mxYUGTg4XFdYWrt4PpYWXne1PFbe9LY3+5n6dbkcV7pys/w+t/Qy+Jed+byc/Tv+O3s6N/kd23h3fldX+HrWmLb1u5eWv/d+F1iavve1+FnaefW1/Fu7N7p7ODe833l2+X76OPwefjqbV1n/H7/6t7h937o3el3c377fXz++/fy593d8GhjcPf4bWRv6d3hfWny083XfG3fz9Tsd+XV1+V+++nj8mZbV1ZUUlJXXV1cW1xfYF9fYmBaVFdo8O3y4M7GxMbEwL69vLy8vsLFx8rP2+Z+VUE6Ojs4MS4vMzQzMzMyMzg9PDYzNjs6NTdPvq6qqaajoqWprK+zuLm4t7m8xNLo8HVMOTExNTUxLzM4ODU1PEhQTUhHSEU/PDo2Ly0tMTQ4ScCqpaisqaSkq7W6t7W0s7K0t7m5vcvqYE0+NTI1NzQyNDg6NzY5P0hPVVZXXXP4X0k9ODQyMDAzQM2vqauuq6WkqrW6trOztLOztbe4vc1pT0pANzIzNjYzMTIzMjIzNjpDVvjr+PHd1+ReST46NzY0Nj/gubCxs6+rqqywtrm5tbGws7e3t7vG2mxQRD07Ojg2NTQyMC8vLy8vMztES05VZ+nY09n4W1JPTEM/RV3VyMO/u7ezsLCytri3tra3ubq8v8PIz+ZkVExHQj88ODUzMi8uLS0uMDM3O0BIVXrb1NPW2Nvqa1pXWl9r9+HWzMK7trSysa+vrq6ur7K2ubzByth7WUxGPzs4NTMwLiwsLC0uMDM2O0FNYvDf2tfY3OhuWVBOT1JYYHvdzMC6trKvrq2rq6qrrK6ws7e9xM3ceFhLQjw4NjIuKyorLCwtLzI3PUlbbvDf2dng8GhWTk1OTk5UYPLWyL+7uLSxrq2sq6urrK2usbW6vsbP4mdQRT47NzEtKiorKywsLjA2PUZOWXHo3tzd6G1eXFpVUlVcbObRycO+urayr66srKurq6ytsLS4vMHL3HFVSUA8Ni8rKSkpKSkqLC81PENLWv3g29re7nRrYlhRT1FZaenYzsjAu7i0sa6trKyrq6ytr7K2u7/I1fZZST86My0qKCgnJygpKy82PERPZujZ0tHW293i82xgXV9u7+DYzsjCvbm2s7Curayrq6ytrrC1ub3F0epdSz86My0pJycmJicoKi0zOj9KWXjf1dPX2drb4Ox9cXvr3dXOy8fDvru5t7SysK6trKytrq+ytrq/ydX1WEg9Ni4rKScmJSUlJysvMzg/TWTs3tvX0s7O1N3i3NPOzs3Iwb6/v7y5uLe1sq+trKurq6utsbnAxc5xT0U+NCoiISUjICMrMjU7Q2DtXFRZVkdJTlvh1tHJvr/Dv8DDwsLFw767t7S0sa+vr6+vr66zuLy/02ZqWUY/PzYoJCUgHiEnKC0zPEhJTGBdSFFZS1Phz8q/vLm5vr28xcnAwsm+ubi3sbGys7S0t7a1trm2u8rP4k9EQTk5LCYpIx4iKCQqMzU6SVNQWVxlT1Rla+HKv766tba8ubnFw7/Iyby7vLWztLS0tbi5t7e4tba7wMvlW0s8QDkqKiogICckJS0yMj1LTE9balRPYmdd1sLBvbS1ure4v8DEyMnKwL29trO4sLK4t7e6uLe2tbq9w+VjVz1BNysrKCEhJSMmLC8yPUtJTWVeT1hhXV/Vyce7uLm5t7y+v8nJxcvGvby5tbO0s7S2uLe2trS0uru/2mdhQD06LSwoIyEkIyUpLzI3Sk1HWfpIT3ZWUtLMz7y3vLe1v7292c7D18q7v7y1trWwtLSzura0ubW2u77J5WZHQzouLiojJCUhJiorMTo9Rk1QUVFTT1lZbdrWx72+ure8v73N3crN17+8wbi1uLSxtbS0uLW1trW3u7/L6VJHPjAuKyciJSUiKC4sOEg9VGpNXGZRW2Be7t7Nw767uLm8v8XT2s/Szb6/vLa3tbCys7G0t7S1tra3vcbZaUY6OS0qKighJiklKzgxPHhIWthVXH1QWmts9dfOysK+v72+y8zT7t7LzL+4uLWwsrCusLGxsrW3t7q9w8vuTUY4MC4pJiclJCgqKzU1QGVOaNNZVepNTXphWtvT28jBxb++ycvO39bOyL25t7Cvsa2usq6xtbO2vLu/zNZmTD42Ly0oJiYjJSgoKjA3OknkXnLMaFDrWErtdmDWzNjGv8C/vL/HxsTJwbq5tLCvrq+vsLKzs7q6usHJyd5cT0A5MC0qJiQkIiQlKCwtNEFIWMzi5MjyVN1fUtnf6cfHyL28vLq6vsDAvr66s7CvrKyurq+1t7vBxs3c5WJRST86NC8tKCYmIyMkJicsLzY+SPjb2srE58/GaN/E4tW7yMK3u7yzt7i1t7e3trOysK2ur6+zubvE0ddmVU5EQT06ODQwLysrKiYnKCYoLC0xO0JP/dHKwr++v8LExsfHwb6+uba4s7C0srC0trS3uri3ubq3u7/Ay+L5XklDQTo5NjIyMS8vLywuLCstLS0vNTc6RlBd3cvBv7y3ubu1ubu1ubi0uLe0t7ezubm0vL66v8LCxcXL1s/tX2BPRD89NzYzNDExNDUzNzsyOTo0NTs5Nz9FR1B18dTPxL/Aurm9u7i7vLu2t7y3t7u8uL/CwMDJ08vPd2zUXUhqXkpHTk8/P0Y/N0M9Nz05PDg9PEU/RlJIXlRM7FpdX9j729rTwufFxsjGvM3Gwcy5zs/CvdbJxO3J0NvL5dHZ6lPE2T/V1WdAbVznR0zVT2FIcE9jSk9dT1pQPNE/fl1E2UrXQMdJ6szhQszTSMtKvD7R3MdPTLA8z+i+VlHHz2xH175DTMXFTjuzVmVR7s5D4ky2Mc/KP8U1tDW5N3y3L7g7uT5exkziW9bENLzOQcw0sE1Awki/Ts1T9r44v1vQX1LIUMlHw19IyVXLPb//Q8Jbzj68Qs5PTLU65dBDuz/eTrYuu9JAb2G+NLstqjxBw2jBLsDPTkjHV8481tf9WVeyO0q1O8pWYrtA/MBwT261NMrFOrY8v1ta1N7EPMJQyE1PtjrKSspo+1Pcy1tOvULNT9veTvXtWla+O8pUz19Duja7Ql/DO7UyvM0qpCitMtm6MK8trUdFwlbsQ81IwkLTSLU/PbJSP7o9wk5HtzjCPbxMZcg4sT5lyvJtcuTPRsnsTufg8mbsZtjtUuS9OsnbTcpKwzyzNN29Obc9vzzKXcpM8U/ESVC4L7f/O8FDy1feQ8/DOcJGw05vespO2mJ042Re1NE6sTztz1jTTcY/uj5gxUXDO7E5ytIypitdrS/CTuzSU9A3rjlLszS+cE3VbX1PxktOsjPG2kvaUeBNw0ZYv2RH3sRIXcZAvDxXu0rRQrxiVtfebOlW39JFy0S8VD+yObgzxr42uTOuNlm+O7s828A9wETXyzq+TM5VZ8lNbcRKxk9dxDe7N7hJSrUvrzi8QM/fN6wu38xSz0O9PLhCTrtQPcrdTco5vWz2U+m/PsZH0NVBy89Z0kG5Te7y0/JPxjq3U0zL4e1MxlTZ/0zBT2v7UdFfZUvLVGTLSt3XUGDWaEm/SlC+R2DU6FHMRMT5T+JVzT66L7rwO7k8xk7QP83XP8Zv1mn4U8xJV2xh0E7j5ctcVr5P++hItzp68ND0QblGv1dOu0neUtfcUt1ezfpPzFXeUWvXW+tWy1Rk3WjZWfz41WVdyvfqb/Lo+2tW1V1ifOvZV2rP7kz26nRsXFrSY0vrzVLhVfTbSfNf2Vff3mTQT97fbXLg6PzsWuXgUXPtZutpX+PcXvzeYvVgVeRqZt/l79zb/tR9Z9tr8Wls6m72bOPj6eZ72vFkbXHpcmzf1/pwempqWlVgcldf+mVtZGr3amBu8Wljaurf6u/dztPPycXFx8jNzu1642po9nt0Y1ZXVkhBPzs5NjQ5PD5HTVNiZmrZzMi9ubSwr66trbG2vdtSRjw2NjRCaUpER0c8Mi80Ozg5QExfYWjfzNHb0cm+vb25tre5ubi6w8i/vcHLy8LE0Nzd+FhNSEZEQEJHSk1TYPzr5trV19PP0tn5T0M8NTQ4OkJQTE1OP0M+NT0/QFZi3sfBxL7Aycvn0snWzsK7vL/Bvb3Izs3P22VUXWNUS0dKT0tJT1JWVU9WV1JPVFdfWk5SW2nz7dfPz9PX0NLSzszFxsrFxsjM3/13Wk5OVGjtfuje7O1pZOjXzMjEvrm4u7y+w8rsaGlWSEBARUQ9PUVGQTs4PkE9PT5GTUI+R1Bfbmfh0+b+8N7Pzc7JxcfKztDNztLQy8rLzM/P093h7nFkXVlUVFlfYl5cWldQT1BTWV945NnRzcvKztbhd15UT05OTlBQUFBOUFhgbXzs39zd3NnX19jV0M3KycnJy87R1NXW2Nzh5OPg3t3c29zf5OXf4e18bF9XT01MS0pLSktNUldZWl5dV1FQUU9NTE9QTkxLTE1LSkpNU1pi+tzPysbDv727urq5t7a2t7e5u8DL2PhdTkZBPz09PT5BREdJTE9XXWh6/Xt7dWxscG1jXFhTUVdbW19scW508+DY1NPSzsvLy8rJycrN0M/O0Nng5+5tXFVPS0dDQUFCQ0NERkhJSk1QU1VXW2Blanb16uDa1dHPz9DT1tfa3uDj4+Ph3dnW1dPRz87Ozs/R1dvl8nRiWE9KR0dISUxRW19icO/p6/RzZmJneurbzsrIycnIyczQ1d3r+Xx4ffTv+3BucGtgWlZQS0ZEQ0FAPz9AQkRHS1FbafHZzcfDwL28u7u7u7y9vsDBw8XIy8/X5ftmV1BMSUdGRkhJSUpKSk1NTU1OUFVZYXPv4d3b1tHPzc3O0NLW2+Dk6PF4aGJfXVtZV1dXV1peaHvw5d7c19TV1tfa3ubs8nlrY1xXUU5NTExLSktMT1JUV1tdXVtcZ/bn497Z1tne4OPr/WhcWVdXVlRUVlVUVFdbYWhx9+Xa1M/Lx8PAvr27u7u7u7y9v8TJz933ZVpTTkxKSktLTE1OTk5OTk5OTU1NTk9QUlRXWl1hZ213fXl4+u7m4NzZ19jZ2tva3OHp8Pb8d3R2cGtmYGBiZWdnanP67efg3NnY2trY1dXW1tTS0dDOzc3Ozs7Ozs/S2OL2a11VT0xJR0RCQUA/Pz4+PT4+P0BDRklLTVBXXmd079/Wz8zJxcPDxMTGx8rMztHT1djb3N7j7P1vZl1XUk9NTExMTU1PUlZZW15hZWhrcvjo39rVz83KyMfGxcXGx8fHyMnLzM7R1dnc4OfxeWtgW1hUUE1LSkhHR0dGRURDQ0NERUdJTVBYZe7a0MzHw8C/vr28vLy9vb2+v8LFyc3U3/RsX1dPS0lHRUNAPz8/Pz9AQ0VHSk1SWmJu//Lr5OHe2tjW1dTRzs3LysjHx8bFxcbGyMvO1Nri/WVbUk1IRENCQD8+Pj49PT4+QEJER0tPWGFu8+Tc2dbSzcvJxsTCwcC/v7+/wMLFyMvMz9Ta4Ox6ZVxYUk5LSEZFRERDQ0RGR0hLT1RYW2Ft+uvj3NfSz87MysjGxsbFxMTFxsfHyczQ1Nfb4erye21iW1dTUE1KR0VEQ0JDRkhISUtNT1NYXWd0fvjs4NrX1tXT0M/Q0c/O0NTW19fa3ujz/XdpX1xcWlRQUFBPTk1MTE1MS0xNTk9RV11mbn306N7a2NTQz9DQz87Ozc3O0NLU19nZ2Nve4+ru8fh2bWtpYFtaW1xaWVlaW1xcXF9maWpvfvHq5d/c2dbV1tbW1dbY2drb3eDm6Ojq7/j9fHVsZmVmZmJgYGFgX2BiZWVjYWJkZGRmbHR6/fLs6uzt6+jm5+fo6evt7Orq6+7w8fHz8/Hy9vv+fHh0cW9ta2lnZmdnZmdpamloa25vbm1tb3N2d3r++vj07uzs7e/z9vf39/j39fLy8/Hv7e3s7e7x9vv8+/17dnNwbmxpaWhnZ2ZnaGhoaGprbG1vc3Z3eXp9/vr49PLy9PPx8PDw7+3s6+rq6urs7Ozr7O3u7/H2+vr5+fr+eXRwcHJ0dnh5ent9/fjz8O7t7e/x8e/u7/H19vb29PLw8PDx8vLz8fDv8PT4/P38+/r8fnp4d3d4eXp6enh3eHl6fH1+/35+fv7+/n58fHx6eHd1dHNycnN0dHNycnNzdnh5eHZ2d3h5eHd2dHNwcHFycXBwb29vcHN3e33+/Pn18vDu7u7v8PHx8vT2+Pn8/n59fn5+fX7+/Pv6+Pb2+Pn5+fr8/X5+fXt6enp5eHd3eHl6e33//v3+/fz7/P3/fX18fHx9fn7/fv79/v7+/358e3l4d3Z1dnZ2d3h6e31+//79/f39/f39/Pv6+fj49/f29/j4+Pn6/Pz9/f7+/v39/f39/f3+/358e3t5eHd1dHJxcHBwcHBvb25tbW1ubm5ubm5ub3FzdXZ2dnZ4eXp7fH19fX19//7+/v7/fn59fv/+/f39/Pz7+/r4+Pj4+Pf39vX19PX19/f29vX19vb29vb29PT19vf4+fr7/f3+/35+fn19fHx8fHx9fX1+fn7//v39/f3+/v//fn18e3p6e3p6enp7e3t8fX1+fn5+fn5+fn59fX18fHt7e3t7e3x9fv79/fv6+fj29fX09PT09fX29vf4+fv8/f3+/v7+/v38/f39/Pz8/Pz8/Pz9/f7+/v9+fX18e3p6enl6e3x8fHx8e3t7fHx8fHx8e3t6e3p7enp6eXl4eHh5eHh4eHh4eHh4d3d3eHh4eHl6enp7fH1+/v38+/z7+/v6+vr6+vn5+vv7+/z8/P38/Pz8/Pz8/Pv8/Pz8+/v8/Pz8/P39/f39/f39/f38/Pz8/Pz8/f39/v3+/v9+fn19fXx7e3p7enp6eXl5eHh4eHh3d3d3d3h3d3d3d3d3eHh4eXp6e3x8fX1+fn7///7+/v7+/v7+/v79/f39/fz8/Pz8/Pz8/Pz8/Pz8+/v7/Pv7/Pz8/f39/f7+/v7+/v39/fz8/Pv7+vn5+fn5+fn5+Pn5+fn5+fn4+fn5+vv7+/z9/f7+fn59fX19fHx8fHx8fHx8fHx7e3t7e3t7e3t7e3t7fHx8fHx8fHx8fHx8fHx8fH19fX19fn5+fn7/fn5+fn5+fn5+fv//fv/+//7/////fn5+fn5+fn5+fv////7+/v7+/v7+/v7+/v/+/v7+/v7+/f7+/v7+//9+fn5+fn5+fn5+fn59fX19fX19fHx8fHx8fHx8fHx8fHx8fHx8fHx7fHt7e3t7e3x8fX19fX19fX59fX1+fn7///7+/v79/f7+/v39/v39/v3+/v7+/v7+/37/fn5+fn59fn5+fn5+fn5+fv/+/v7+/v39/f38/Pz8/Pz8+/z8+/v7+/v7+/v7+/v7/Pv7+/v7+/z8/Pz9/Pz9/f39/f3+/f79/v7+/v7+/v7+fv9+fn5+fn19fHx8fHx8e3t7e3t7e3x7e3t7e3t8e3x8fHx8fX19fX1+fX5+fn5+fn5+fn7/fn5+fn5+fX19fX18fXx8fHx8e3t8e3t8e3t7fHt8fHx8fH19fX1+fX5+fv9+//7///7+/v7+/v39/f39/f39/f39/f3+/f39/Pz8/Pz9/Pz8/Pz8/Pz8+/v7/Pz8/f3+/f79/v3+/v7+/v/+/v7+/v////9+fn5+fn5+fn5+fn5+fn5+fn5+//9+/v7//v7////+////fn5+fX59fX19fX19fX18fX18fX19fXx9fXx8fHx8fHt8e3x7e3x7fHt8fHt8fHx7fHt8fHx8fHx8fXx9fX19fX1+fv/+//7+/v39/f38/Pv8+/v7+/v7+/v7/Pz8+/v7+/v7+vr6+/v7+/v7/Pz8/f39/v7/fn5+fn59fX59fX19fn5+fn1+fn19fX19fX19fX19fX1+fn5+fv/+fv5+/35+fn59fX18fHx8fHx8fH18fH59fX5+fn7///7///9+/35+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn59fXx9fXx9fX19fX1+fn5+fv///v7+/v7+/v7+/f3+/f3+/f39/fz8/Pv8/Pz8/Pz8/Pz7/Pv8/Pz8/Pz8/Pz8/P39/f39/f39/f3+/f7+/v//fn5+fn19fXx8fHx8e3x7e3t7enp6eXl5eXl4eHh4eHh5eXl5eXl5eXl6enp6e3t7fHx8fHx9fX5+fv7+/v39/Pz8/Pz7+/v7+/v7+/v7+/v7+/v7/Pz8/Pz8/Pz8/Pv8+/v7+/v7+/v7+/v7+vr7+vr6+vr6+vr7+/z9/f7+//9+fn59fXx8fHx8fHt7e3x7fHx8fH19fX1+fX5+fn5+fn5+fn1+fn5+fn59fX19fHx8e3t7e3t6e3t7ent7e3p6e3t6enp6enp6enp6enp6e3t7e3t7e3x8fHx9fX1+fv/+/v7+/f39/Pz7+/r7+vr6+fn5+fn5+fn5+fn6+vr6+vr6+vr7+/v7+/z7/Pz8/Pz8/P38/P39/f7+//9+fn5+fX19fX19fXx8fX19fX19fX19fn5+fn59fX19fHx8e3t7e3t7enp6ent7e3t8fHx8fH19fX19fX19fX5+fv///v7+/v39/fz8/Pz8/Pz8/Pz9/f39/f39/f39/Pz8/Pv7+/v7+/v7+/v7/Pz8/P39/f79/f7+/v7+////fn5+fn19fXx8fHt7e3p6enl5eXl5enl6enp6ent7e3t8e3x8fHx9fX19fX5+fn5+///+/v7+/v7+/v///35+fn59fX18fHt7e3t6enl6eXl5eXl5eXp6ent7fHx9fn7//v79/fz8/Pv7+/v7+vv7+vv6+/v7+/v7+/v7+/v7+/v7+/v8/Pv8+/v7/Pz8/Pz8/P39/f39/v7+//9+////fn5+fn5+fn1+fX19fn5+fn5+fn7////+/v39/Pz8+/v7+/v7+/v7/Pz8/f7+/35+fn59fX19fX19fX19fXx8fHx7e3t7e3t8e3t8fHt7fHx8fHx8fH19fv/+/v7///37/P36+f7+/A=="  }.GetRandomElement();
        
        var holdOnAudio = new
        {
            @event = "media",
            streamSid = _aiSpeechAssistantStreamContext.StreamSid,
            media = new { payload = holdOn }
        };

        await SendToWebSocketAsync(twilioWebSocket, holdOnAudio, cancellationToken);

        var systemPrompt = "You are a telephone recording analyst who is fluent in Chinese and can accurately and completely repeat the items that customers need to order.\n\nThe following is a list of items on sale:\n\n1. thirty-one/thirty-five shrimp  \n2.twenty-one/twenty-five shrimp \n3.twenty/thirty shrimp \n4. nine/twelve shrimp \n5. thirty-one/forty & thirty-six/forty shrimp \n6. golden pomfret  \n7. imitation crab stick  \n8. tilapia  \n9. tilapia fillet  \n10. round scad  \n11. catfish (gutted)  \n12.Chicken breast  \n13. whole-thigh mt skinless boneless  \n14.eggs  \n15. headless duck  \n16. beef eye round  \n17. beef brisket  \n18. cut beef bone  \n19. beef xl flank  \n20. beef ribeye \n21. beef tendon  \n22. beef tendon ball  \n23. boneless pork butt\n24. bone-in pork butt\n25. pork bone  \n26. pork loin  \n27. medium & light sparerib  \n28. bean sprout  \n29. green leaf  \n30. celery  \n31. lettuce  (also known as iceberg lettuce)\n32. broccoli  \n33. cabbage  \n34. bok choy  \n35. shanghai bok choy  \n36. peeled garlic  \n37. green onion  \n38. ginger  \n39. jalapeno  \n40. cilantro  \n41. green bell pepper  \n42. yellow onion  \n43. cucumber  \n44. taro  \n45. yam  \n46. carrot  \n47. daikon  \n48. lime  \n49. lemon  \n50. orange  \n51.chix bone  \n52. pho noodle  \n53. rice noodle  (Also known as chow fun)\n54. all purpose flour  \n55. salt  \n56. msg  \n57. white granulated sugar  \n58. baking soda  \n59. corn starch  \n60. white vinegar  \n61. oyster sauce  \n62. soy sauce  \n63. hoisin sauce  \n64. chili sauce  \n65. soy sauce pack  \n66. sriracha pack  \n67. hoisin pack  \n68. dried chili (crushed)  \n69. baking powder  \n70. vegetable oil  \n71. canned pineapple juice  \n72. canned mushroom slices  \n73. canned ketchup  \n74. canned baby corn (whole)  \n75. fork  \n76. spoon  \n77. chopstick  \n78. foam box_3 comp  \n79. portion cup 1oz  \n80. portion lid 1oz  \n81. napkin to go  \n82. diner check/takeaway order book(if customer mention check order means he want takeaway order book so please put into the order)\n83. calendar  \n84. spring roll wrappers  \n85. won ton wrappers (thin)  \n86. sparkling water  (Also known as COCO RICO DRINK)\n87. medium three compartments\n88. large three compartments";            
        
        var prompt = "Help me to repeat the order completely, quickly and naturally in English:";
        
        using var memoryStream = new MemoryStream();

        await using (var writer = new WaveFileWriter(memoryStream, new WaveFormat(8000, 16, channels: 1)))
        {
            List<byte[]> bufferBytesCopy;

            lock (_wholeAudioBufferBytes)
            {
                // 创建集合的副本
                bufferBytesCopy = _wholeAudioBufferBytes.ToList();
            }

            // 对副本进行枚举
            foreach (var audio in bufferBytesCopy)
            {
                for (int index = 0; index < audio.Length; index++)
                {
                    var t = audio[index];
                    var pcmSample = MuLawDecoder.MuLawToLinearSample(t);
                    writer.WriteSample(pcmSample / 32768f);
                }
            }
        }
        
        var fileContent = memoryStream.ToArray();
        var audioData = BinaryData.FromBytes(fileContent);

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
                temperature = _openAiSettings.RealtimeTemperature,
                input_audio_transcription = assistant.Id == 176 ? new { model = "gpt-4o-transcribe" } : new { model = "whisper-1" },
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
                temperature = _openAiSettings.RealtimeTemperature,
                input_audio_transcription = new { model = "whisper-1" },
                tools = configs.Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool).Select(x => x.Config)
            },
            _ => throw new NotSupportedException(nameof(assistant.ModelProvider))
        };
    }

    private async Task<List<(AiSpeechAssistantSessionConfigType Type, object Config)>> InitialSessionConfigAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken = default)
    {
        var functions = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdAsync(assistant.Id, assistant.ModelProvider, true, cancellationToken).ConfigureAwait(false);

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