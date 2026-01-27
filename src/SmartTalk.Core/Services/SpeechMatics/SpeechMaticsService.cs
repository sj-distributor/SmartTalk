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
                        "你需要帮我从电话录音报告中判断两个维度：\n" +
                        "1. 是否真人接听（IsHumanAnswered）：\n" +
                        "   - 默认返回 true，表示是真人接听。\n" +
                        "   - 当报告中包含转接语音信箱、系统提示、无人接听，或是 是AI 回复时，返回 false。表示非真人接听\n" +
                        "例子：" +
                        "“转接语音信箱“，“非真人接听”，“无人应答”，“对面为重复系统音提示”\n" +
                        "2. 客人态度是否友好（IsCustomerFriendly）：\n" +
                        "   - 如果语气平和、客气、积极配合，返回 true。\n" +
                        "   - 如果语气恶劣、冷淡、负面或不耐烦，返回 false。\n" +
                        "输出格式务必是 JSON：\n" +
                        "{\"IsHumanAnswered\": true, \"IsCustomerFriendly\": true}\n" +
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

    private async Task<DialogueScenarioResultDto> IdentifyDialogueScenariosAsync(string query, CancellationToken cancellationToken)
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
                            "请根据交谈主题以及交谈该内容，将其精准归类到下述预定义类别中。\n\n" +
                            "### 可用分类（严格按定义归类，每个类别对应核心业务场景）：\n" +
                            "1. Reservation（预订）\n   " +
                            "- 顾客明确请求预订餐位，并提供时间、人数等关键预订信息。\n" +
                            "2. Order（下单）\n   " +
                            "- 顾客有明确购买意图，发起真正的下单请求（堂食、自取、餐厅直送外卖），包含菜品、数量等信息；\n " +
                            "- 本类别排除对第三方外卖平台订单的咨询/问题类内容。\n" +
                            "3. Inquiry（咨询）\n   " +
                            "- 针对餐厅菜品、价格、营业时间、菜单、下单金额、促销活动、开票可行性等常规信息的提问；\n   " +
                            "4. ThirdPartyOrderNotification（第三方订单相关）\n   " +
                            "- 核心：**只要交谈中提及「第三方外卖平台名称/订单标识」，无论场景（咨询、催单、确认），均优先归此类**；\n   " +
                            "- 平台范围：DoorDash、Uber Eats、Grubhub、Postmates、Caviar、Seamless、Fantuan（饭团外卖）、HungryPanda（熊猫外卖）、EzCater，及其他未列明的“非餐厅自有”外卖平台；\n   " +
                            "- 场景包含：查询平台订单进度、催单、确认餐厅是否收到平台订单、平台/骑手通知等。\n " +
                            "5. ComplaintFeedback（投诉与反馈）\n " +
                            " - 顾客针对食物、服务、配送、餐厅体验提出的投诉或正向/负向反馈。\n" +
                            "6. InformationNotification（信息通知）\n   " +
                            "- 核心：「无提问/请求属性，仅传递事实性信息或操作意图」，无需对方即时决策；\n " +
                            " 细分场景：\n" +
                            " - 餐厅侧通知：“您点的菜缺货”“配送预计20分钟后到”“今天停水无法做饭”；\n    " +
                            " - 顾客侧通知：“我预订的餐要迟到1小时”“原本4人现在改2人”“我取消今天到店”“我想把堂食改外带”；\n    " +
                            " - 外部机构通知：“物业说明天停电”“城管通知今天不能外摆”；" +
                            "7. TransferToHuman（转人工）\n" +
                            " - 提及到人工客服，转接人工服务的场景。\n" +
                            "8. SalesCall（推销电话）\n" +
                            "- 外部公司（保险、装修、广告等）的促销/销售类来电。\n" +
                            "9. InvalidCall（无效通话）\n" +
                            "- 无实际业务内容的通话：静默来电、无应答、误拨、挂断、无法识别的噪音，或仅出现“请上传录音”“听不到”等无意义话术。\n" +
                            "10. TransferVoicemail（语音信箱）\n    " +
                            "- 通话提及到语音信箱的场景。\n" +
                            "11. Other（其他）\n   " +
                            "- 无法归入上述10类的内容，需在'remark'字段补充简短关键词说明。\n\n" +
                            "### 输出规则（禁止输出任何额外文本，仅返回JSON）：\n" +
                            "必须返回包含以下2个字段的JSON对象，格式如下：\n" +
                            "{\n  \"category\": \"取值范围：Reservation、Order、Inquiry、ThirdPartyOrderNotification、ComplaintFeedback、InformationNotification、TransferToHuman、SalesCall、InvalidCall、TransferVoicemail、Other\",\n " +
                            " \"remark\": \"仅当category为'Other'时填写简短关键词（如‘咨询加盟’），其余类别留空\"\n" +
                            " \"IsIncludeTodo\": \"默认为false; 当报告中列出todo，或者待办事项时为ture，否则为false\"\n}" +
                            "当一个对话中有多个场景出现时，需要遵循以下的识别优先级：" +
                            "*Order > Reservation/InformationNotification > Inquiry > ComplaintFeedback > TransferToHuman > TransferVoicemail > ThirdPartyOrderNotification > SalesCall > InvalidCall > Other*"
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