using System.Text.Json;
using System.Text.RegularExpressions;
using Google.Cloud.Translation.V2;
using Serilog;
using SmartTalk.Core.Ioc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Domain.AISpeechAssistant;
using Twilio.Rest.Api.V2010.Account;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.WeChat;
using SmartTalk.Core.Settings.PhoneOrder;
using SmartTalk.Core.Domain.SpeechMatics;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.Sales;
using SmartTalk.Messages.Enums.STT;
using Twilio;
using Exception = System.Exception;
using JsonSerializer = System.Text.Json.JsonSerializer;
using SmartTalk.Core.Settings.SpeechMatics;
using SmartTalk.Messages.Enums.SpeechMatics;

namespace SmartTalk.Core.Services.SpeechMatics;

public interface ISpeechMaticsService : IScopedDependency
{
    Task<string> CreateSpeechMaticsJobAsync(byte[] recordContent, string recordName, string language, SpeechMaticsJobScenario scenario, CancellationToken cancellationToken);
}

public class SpeechMaticsService : ISpeechMaticsService
{
    private readonly ISalesClient _salesClient;
    private readonly IWeChatClient _weChatClient;
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly TranslationClient _translationClient;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    
    private readonly ISpeechMaticsClient _speechMaticsClient;
    private readonly SpeechMaticsKeySetting _speechMaticsKeySetting;
    private readonly ISpeechMaticsDataProvider _speechMaticsDataProvider;
    private readonly TranscriptionCallbackSetting _transcriptionCallbackSetting;

    public SpeechMaticsService(
        ISalesClient salesClient,
        IWeChatClient weChatClient,
        OpenAiSettings openAiSettings,
        TwilioSettings twilioSettings,
        TranslationClient translationClient,
        ISmartiesClient smartiesClient,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        ISpeechMaticsClient speechMaticsClient,
        SpeechMaticsKeySetting speechMaticsKeySetting,
        ISpeechMaticsDataProvider speechMaticsDataProvider,
        TranscriptionCallbackSetting transcriptionCallbackSetting)
    {
        _salesClient = salesClient;
        _weChatClient = weChatClient;
        _openAiSettings = openAiSettings;
        _twilioSettings = twilioSettings;
        _translationClient = translationClient;
        _smartiesClient = smartiesClient;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _speechMaticsClient = speechMaticsClient;
        _speechMaticsKeySetting = speechMaticsKeySetting;
        _speechMaticsDataProvider = speechMaticsDataProvider;
        _transcriptionCallbackSetting = transcriptionCallbackSetting;
    }

    public async Task<string> CreateSpeechMaticsJobAsync(byte[] recordContent, string recordName, string language, SpeechMaticsJobScenario scenario, CancellationToken cancellationToken)
    {
        var retryCount = 2;

        while (true)
        {
            var transcriptionJobIdJObject = JObject.Parse(await CreateTranscriptionJobAsync(recordContent, recordName, language, cancellationToken).ConfigureAwait(false));

            var transcriptionJobId = transcriptionJobIdJObject["id"]?.ToString();

            Log.Information("Phone order record transcriptionJobId: {@transcriptionJobId}", transcriptionJobId);

            if (transcriptionJobId != null)
            {
                var speechMaticsJob = new SpeechMaticsJob
                {
                    Scenario = scenario,
                    JobId = transcriptionJobId,
                    CallbackUrl = _transcriptionCallbackSetting.Url
                };
                
                await _speechMaticsDataProvider.AddSpeechMaticsJobAsync(speechMaticsJob, true, cancellationToken).ConfigureAwait(false);
                
                return transcriptionJobId;
            }

            Log.Information("Create speechMatics job abnormal, start replacement key");

            var keys = await _speechMaticsDataProvider.GetSpeechMaticsKeysAsync(
                    [SpeechMaticsKeyStatus.Active, SpeechMaticsKeyStatus.NotEnabled], cancellationToken: cancellationToken).ConfigureAwait(false);

            Log.Information("Get speechMatics keys：{@keys}", keys);

            var activeKey = keys.FirstOrDefault(x => x.Status == SpeechMaticsKeyStatus.Active);

            var notEnabledKey = keys.FirstOrDefault(x => x.Status == SpeechMaticsKeyStatus.NotEnabled);

            if (notEnabledKey != null && activeKey != null)
            {
                notEnabledKey.Status = SpeechMaticsKeyStatus.Active;
                notEnabledKey.LastModifiedDate = DateTimeOffset.Now;
                activeKey.Status = SpeechMaticsKeyStatus.Discard;
            }

            Log.Information("Update speechMatics keys：{@keys}", keys);

            await _speechMaticsDataProvider.UpdateSpeechMaticsKeysAsync([notEnabledKey, activeKey], cancellationToken: cancellationToken).ConfigureAwait(false);

            retryCount--;

            if (retryCount <= 0)
            {
                await _weChatClient.SendWorkWechatRobotMessagesAsync(
                    _speechMaticsKeySetting.SpeechMaticsKeyEarlyWarningRobotUrl,
                    new SendWorkWechatGroupRobotMessageDto
                    {
                        MsgType = "text",
                        Text = new SendWorkWechatGroupRobotTextDto
                        {
                            Content = $"SMT Speech Matics Key Error"
                        }
                    }, cancellationToken).ConfigureAwait(false);

                return null;
            }

            Log.Information("Retrying Create Speech Matics Job Attempts remaining: {RetryCount}", retryCount);
        }
    }

    private async Task<string> CreateTranscriptionJobAsync(byte[] data, string fileName, string language, CancellationToken cancellationToken)
    {
        var createTranscriptionDto = new SpeechMaticsCreateTranscriptionDto { Data = data, FileName = fileName };

        var jobConfigDto = new SpeechMaticsJobConfigDto
        {
            Type = SpeechMaticsJobType.Transcription,
            TranscriptionConfig = new SpeechMaticsTranscriptionConfigDto
            {
                Language = SelectSpeechMetisLanguageType(language),
                Diarization = SpeechMaticsDiarizationType.Speaker,
                OperatingPoint = SpeechMaticsOperatingPointType.Enhanced
            },
            NotificationConfig = new List<SpeechMaticsNotificationConfigDto>
            {
                new SpeechMaticsNotificationConfigDto
                {
                    AuthHeaders = _transcriptionCallbackSetting.AuthHeaders,
                    Contents = new List<string> { "transcript" },
                    Url = _transcriptionCallbackSetting.Url
                }
            }
        };

        return await _speechMaticsClient.CreateJobAsync(new SpeechMaticsCreateJobRequestDto { JobConfig = jobConfigDto }, createTranscriptionDto, cancellationToken).ConfigureAwait(false);
    }
    
    private SpeechMaticsLanguageType SelectSpeechMetisLanguageType(string language)
    {
        return language switch
        {
            "en" => SpeechMaticsLanguageType.En,
            "zh" => SpeechMaticsLanguageType.Yue,
            "zh-CN" or "zh-TW" => SpeechMaticsLanguageType.Cmn,
            "es" => SpeechMaticsLanguageType.Es,
            "ko" => SpeechMaticsLanguageType.Ko,
            _ => SpeechMaticsLanguageType.En
        };
    }

    private async Task<DialogueScenarioResultDto> IdentifyDialogueScenariosAsync(string query, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
            {
                Messages = new List<CompletionsRequestMessageDto>
                {
                    new()
                    {
                        Role = "system",
                        Content = new CompletionsStringContent(
                            "You are a professional restaurant AI call analysis assistant. " +
                            "After each customer call, your job is to classify the main scenario of the call into one of the predefined categories. " +
                            "Choose only ONE category according to the following priority order:\n\n" +
                            "1. Reservation - Customer requests to book a table, specify time or number of people.\n" +
                            "2. Order - Customer wants to place, modify, or check an order.\n" +
                            "3. Inquiry - Customer asks about dishes, prices, opening hours, promotions, etc.\n" +
                            "4. ThirdPartyOrderNotification - Calls from delivery platforms or third parties notifying orders or issues.\n" +
                            "5. ComplaintFeedback - Customer complains or gives feedback about service or food.\n" +
                            "6. InformationNotification - Restaurant proactively informs customers about events or reminders.\n" +
                            "7. TransferToHuman - AI transferred the call to a human staff.\n" +
                            "8. SalesCall - Promotional or sales calls from external companies.\n" +
                            "9. InvalidCall - Silent calls, wrong numbers, or hang-ups.\n" +
                            "10. TransferVoicemail- The call was forwarded to voicemail." +
                            "11. Other - Anything that cannot clearly fit the above categories. In this case, extract a short key dialogue snippet as remark.\n\n" +
                            "When multiple intents appear, select the one with the highest priority.\n" +
                            "Output strictly in JSON format with two fields only:\n" +
                            "{\"category\": \"one of [Reservation, Order, Inquiry, ThirdPartyOrderNotification, ComplaintFeedback, InformationNotification, TransferToHuman, SalesCall, InvalidCall, Other]\", " +
                            "\"remark\": \"if category is 'Other', include a short dialogue snippet, otherwise leave empty\"}. " +
                            "No explanation or extra text — output only the JSON object.")
                    },
                    new()
                    {
                        Role = "user",
                        Content = new CompletionsStringContent($"Call transcript: {query}\nOutput:")
                    }
                },
                Model = OpenAiModel.Gpt4o, ResponseFormat = new() { Type = "json_object" }
            }, cancellationToken).ConfigureAwait(false);
        
        var response = completionResult.Data.Response?.Trim();
        
        var result = JsonConvert.DeserializeObject<DialogueScenarioResultDto>(response);
        
        if (result == null) throw new Exception($"IdentifyDialogueScenariosAsync 无法反序列化模型返回结果: {response}");
        
        return result;
    }
    
}