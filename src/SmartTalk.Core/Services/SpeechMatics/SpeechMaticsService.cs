using Google.Cloud.Translation.V2;
using Serilog;
using SmartTalk.Core.Ioc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.WeChat;
using SmartTalk.Core.Settings.PhoneOrder;
using SmartTalk.Core.Domain.SpeechMatics;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Dto.PhoneOrder;
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
    
    private async Task UpdateRecordOrderIdAsync(PhoneOrderRecord record, Guid orderId, CancellationToken cancellationToken) 
    { 
        var orderIds = string.IsNullOrEmpty(record.OrderId) ? new List<Guid>() : JsonSerializer.Deserialize<List<Guid>>(record.OrderId)!;

        orderIds.Add(orderId); 
        record.OrderId = JsonSerializer.Serialize(orderIds);

        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(bool IsHumanAnswered, bool IsCustomerFriendly)> CheckCustomerFriendlyAsync(string transcriptionText, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new()
                {
                    Role = "system",
                    Content = new CompletionsStringContent(
                        "你需要帮我从电话录音报告中判断两个维度：" +
                        "1. 是否真人接听（IsHumanAnswered）：" +
                        "   - 如果客户有自然对话、提问、回应、表达等语气，说明是真人接听，返回 true。" +
                        "   - 如果是语音信箱、系统提示、无人应答，返回 false。" +
                        "2. 客人态度是否友好（IsCustomerFriendly）：" +
                        "   - 如果语气平和、客气、积极配合，返回 true。" +
                        "   - 如果语气恶劣、冷淡、负面或不耐烦，返回 false。" +
                        "输出格式务必是 JSON：" +
                        "{\"IsHumanAnswered\": true, \"IsCustomerFriendly\": true}" +
                        "\n\n样例：\n" +
                        "input: 通話主題：客戶查詢價格。\n內容摘要：客戶開場問候並詢問價格，語氣平和，最後表示感謝。\noutput: {\"IsHumanAnswered\": true, \"IsCustomerFriendly\": true}\n" +
                        "input: 通話主題：外呼無人接聽。\n內容摘要：撥號後自動語音提示‘您撥打的電話暫時無法接通’。\noutput: {\"IsHumanAnswered\": false, \"IsCustomerFriendly\": false}\n"
                    )
                },
                new()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"input: {transcriptionText}, output:")
                }
            },
            Model = OpenAiModel.Gpt4o,
            ResponseFormat = new() { Type = "json_object" }
        }, cancellationToken).ConfigureAwait(false);

        var response = completionResult.Data.Response?.Trim();

        var result = JsonConvert.DeserializeObject<PhoneOrderCustomerAttitudeAnalysis>(response);

        if (result == null) throw new Exception($"无法反序列化模型返回结果: {response}");

        return (result.IsHumanAnswered, result.IsCustomerFriendly);
    }

    private async Task<DialogueScenarioResultDto> IdentifyDialogueScenariosAsync(string query,
        CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync(
            new AskGptRequest
            {
                Messages = new List<CompletionsRequestMessageDto>
                {
                    new()
                    {
                        Role = "system",
                        Content = new CompletionsStringContent(
                            "You are a professional restaurant AI call analysis assistant. " +
                            "After each customer call, your task is to classify the main scenario of the call into one of the predefined categories.\n\n" +
                            "Below are the available categories:\n" +
                            "1. Reservation - The customer requests to book a table, specifying a time or number of people.\n" +
                            "2. Order - The customer wants to place an order directly with the restaurant (dine-in, pickup, or restaurant delivery), " +
                            "or modify the order before preparation (add/remove items, cancel order, issue invoices, etc.). " +
                            "This category does NOT include inquiries or issues about third-party delivery platform orders.\n" +
                            "3. Inquiry - General questions about restaurant dishes, prices, opening hours, promotions, etc.\n" +
                            "4. ThirdPartyOrderNotification - Any conversation related to third-party delivery platforms, including but not limited to: " +
                            "DoorDash, Uber Eats, Grubhub, Postmates, Caviar, Seamless, Fantuan（飯團外賣）, HungryPanda（熊貓外賣）, EzCater. " +
                            "Includes customers checking the status of platform orders, asking whether the restaurant received their order, " +
                            "requesting to expedite a platform order, or calls from platforms/couriers about issues.\n" +
                            "5. ComplaintFeedback - Customer complaints or feedback about food, service, delivery issues, or restaurant experience.\n" +
                            "6. InformationNotification - One-way notifications, such as out-of-stock messages, delivery time notifications, reminders, etc.\n" +
                            "7. TransferToHuman - The AI transfers or attempts to transfer the call to a human agent.\n" +
                            "8. SalesCall - Promotional or sales calls from external companies (insurance, renovation, advertising, etc.).\n" +
                            "9. InvalidCall - No meaningful conversation: silent calls, no response, wrong number, accidental dial, or hang-ups.\n" +
                            "10. TransferVoicemail - The call was transferred to voicemail.\n" +
                            "11. Other - Anything that cannot clearly be categorized above. Provide a short key snippet in 'remark'.\n\n" +
                            "When multiple intents appear, choose the one with the highest priority using this order:\n" +
                            "TransferVoicemail > TransferToHuman > Reservation > Order > Inquiry > ComplaintFeedback > InformationNotification > ThirdPartyOrderNotification > SalesCall > InvalidCall > Other\n\n" +
                            
                            "Output STRICTLY in JSON format with exactly two fields:\n" +
                            "{\"category\": \"one of [Reservation, Order, Inquiry, ThirdPartyOrderNotification, ComplaintFeedback, InformationNotification, TransferToHuman, SalesCall, InvalidCall, Other]\"," +
                            " \"remark\": \"If category is 'Other', include a short snippet; otherwise leave empty\"}.\n" +
                            "No explanations or extra text — return only the JSON object."
                        )
                    },
                    new()
                    {
                        Role = "user",
                        Content = new CompletionsStringContent($"Call transcript: {query}\nOutput:")
                    }
                },
                Model = OpenAiModel.Gpt4o,
                ResponseFormat = new() { Type = "json_object" }
            },
            cancellationToken
        ).ConfigureAwait(false);

        var response = completionResult.Data.Response?.Trim();

        var result = JsonConvert.DeserializeObject<DialogueScenarioResultDto>(response);

        if (result == null)
            throw new Exception($"IdentifyDialogueScenariosAsync 无法反序列化模型返回结果: {response}");

        return result;
    }
}