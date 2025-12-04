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
                            "根据电话录音内容归类到预定义的类别之一\n" +
                           "以下是可用的分类：\n" +
                           "1. Reservation（预订） " +
                           "- 顾客请求预订餐位，并指定时间或人数。 \n" +
                           "2. Order（下单）" +
                           " - 顾客希望直接向餐厅下单（堂食、自取或外卖）" +
                           "- 此类别不包括关于第三方外卖平台订单的咨询或问题。\n" +
                           "3. Inquiry（咨询） " +
                           "- 关于餐厅菜品、价格、营业时间、菜品、菜单、营业时间、下单金额，促销活动,咨询是否可以开发票等的常规问题。\n" +
                           "4. ThirdPartyOrderNotification（第三方订单相关） - 任何提及到第三方平台的订单对话，包括但不限于" +
                           "-DoorDash、Uber Eats、Grubhub、Postmates、Caviar、Seamless、Fantuan（饭团外卖）、HungryPanda（熊猫外卖）、EzCater。对话中有此类关键词，都为ThirdPartyOrderNotification类别" +
                           "-包含顾客查询平台订单进度、询问餐厅是否收到订单、催单、或来自平台/骑手的通知或问题。\n" +
                           "5. ComplaintFeedback（投诉与反馈） " +
                           "- 顾客对食物、服务、配送问题或餐厅体验的投诉或反馈。\n" +
                           "6. InformationNotification（信息通知） " +
                           "- 单向通知，例如缺货通知、订货通知，配送时间通知、提醒，客人要求开具发票等\n" +
                           "7. TransferToHuman（转人工） " +
                           "- AI 将电话转接或尝试转接给人工客服。\n" +
                           "8. SalesCall（推销电话）" +
                           " - 来自外部公司（保险、装修、广告等）的促销或销售电话。\n" +                                                                                                                                                                                                                                                                                                                                                              
                           "9. InvalidCall（无效通话）" +
                           " - 无意义的通话：静默来电、用户无呼应，无应答、拨错号码、误拨或挂断\n" +
                           "10. TransferVoicemail（语音信箱） " +
                           "- 通话被转入语音信箱。\n" +
                           "11. Other（其他）" +
                           " - 无法明确归类的内容。需在 'remark' 字段提供简短关键词描述。\n\n" +
                           "输出必须严格遵循 JSON 格式，且仅包含以下两个字段：\n" +
                           "{\"category\": \"必须为以下之一：[Reservation, Order, Inquiry, ThirdPartyOrderNotification, ComplaintFeedback, InformationNotification, TransferToHuman, SalesCall, InvalidCall, Other]\"," +
                           " \"remark\": \"如果 category 为 'Other'，请提供简短说明；否则留空\"}。\n" +
                           "禁止输出任何额外说明或文本 —— 只返回 JSON 对象。"
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