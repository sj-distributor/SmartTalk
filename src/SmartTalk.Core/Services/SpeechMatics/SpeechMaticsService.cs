using System.Text.Json;
using System.Text.RegularExpressions;
using AutoMapper;
using Google.Cloud.Translation.V2;
using Microsoft.Extensions.Azure;
using Serilog;
using SmartTalk.Core.Ioc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.PhoneOrder;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.Sales;
using SmartTalk.Messages.Enums.STT;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Exception = System.Exception;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.SpeechMatics;

public interface ISpeechMaticsService : IScopedDependency
{
    Task HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken);
}

public class SpeechMaticsService : ISpeechMaticsService
{
    private readonly IMapper _mapper;
    private readonly ISalesClient _salesClient;
    private readonly  IWeChatClient _weChatClient;
    private  readonly IFfmpegService _ffmpegService;
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly TranslationClient _translationClient;
    private readonly ISmartiesClient _smartiesClient;
    private readonly PhoneOrderSetting _phoneOrderSetting;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly ISalesPhoneOrderPushService _salesPhoneOrderPushService;
    private readonly ISmartTalkHttpClientFactory _smartTalkHttpClientFactory;
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    
    public SpeechMaticsService(
        IMapper mapper,
        ISalesClient salesClient,
        IWeChatClient weChatClient,
        IFfmpegService ffmpegService,
        OpenAiSettings openAiSettings,
        TwilioSettings twilioSettings,
        TranslationClient translationClient,
        ISmartiesClient smartiesClient,
        PhoneOrderSetting phoneOrderSetting,
        IPhoneOrderService phoneOrderService,
        ISalesDataProvider salesDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        ISalesPhoneOrderPushService salesPhoneOrderPushService,
        ISmartTalkHttpClientFactory smartTalkHttpClientFactory,
        ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _mapper = mapper;
        _salesClient = salesClient;
        _weChatClient = weChatClient;
        _ffmpegService = ffmpegService;
        _openAiSettings = openAiSettings;
        _twilioSettings = twilioSettings;
        _translationClient = translationClient;
        _smartiesClient = smartiesClient;
        _phoneOrderSetting = phoneOrderSetting;
        _phoneOrderService = phoneOrderService;
        _salesDataProvider = salesDataProvider;
        _backgroundJobClient = backgroundJobClient;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _salesPhoneOrderPushService = salesPhoneOrderPushService;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public async Task HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken)
    {
        if (command.Transcription == null || command.Transcription.Job == null || command.Transcription.Job.Id.IsNullOrEmpty()) return;

        var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByTranscriptionJobIdAsync(command.Transcription.Job.Id, cancellationToken).ConfigureAwait(false);

        Log.Information("Get Phone order record : {@record}", record);
        
        if (record == null) return;
        
        Log.Information("Transcription results : {@results}", command.Transcription.Results);
        
        try
        {
            record.Status = PhoneOrderRecordStatus.Transcription;
            
            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);
            
            var speakInfos = StructureDiarizationResults(command.Transcription.Results);

            var audioContent = await _smartTalkHttpClientFactory.GetAsync<byte[]>(record.Url, cancellationToken).ConfigureAwait(false);
            
            await _phoneOrderService.ExtractPhoneOrderRecordAiMenuAsync(speakInfos, record, audioContent, cancellationToken).ConfigureAwait(false);
            
            await SummarizeConversationContentAsync(record, audioContent, cancellationToken).ConfigureAwait(false);
            
            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            _smartTalkBackgroundJobClient.Enqueue<IPhoneOrderProcessJobService>(x => x.CalculateRecordingDurationAsync(record, null, cancellationToken), HangfireConstants.InternalHostingFfmpeg);
        }
        catch (Exception e)
        {
            record.Status = PhoneOrderRecordStatus.Exception;
            
            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);

            Log.Warning("Handle transcription callback failed: {@Exception}", e);
        }
    }
    
    private async Task SummarizeConversationContentAsync(PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken)
    {
        var (aiSpeechAssistant, agent) = await _aiSpeechAssistantDataProvider.GetAgentAndAiSpeechAssistantAsync(record.AgentId, record.AssistantId, cancellationToken).ConfigureAwait(false);

        Log.Information("Get Assistant: {@Assistant} and Agent: {@Agent} by agent id {agentId}", aiSpeechAssistant, agent, record.AgentId);
        
        var callFrom = string.Empty;
        var callTo = string.Empty;
        
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

        try
        {
            await RetryAsync(async () =>
            {
                var call = await CallResource.FetchAsync(record.SessionId);
                callFrom = call?.From;
                callTo = call?.To;
                Log.Information("Fetched incoming phone number from Twilio: {callFrom}", callFrom);
            }, maxRetryCount: 3, delaySeconds: 3, cancellationToken);
        }
        catch (Exception e)
        {
            Log.Warning("Fetched incoming phone number from Twilio failed: {Message}", e.Message);
        }
        
        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");

        var messages = await ConfigureRecordAnalyzePromptAsync(agent, aiSpeechAssistant, callFrom ?? "", currentTime, audioContent, cancellationToken);
        
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);
 
        ChatCompletionOptions options = new() { ResponseModalities = ChatResponseModalities.Text };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        Log.Information("sales record analyze report:" + completion.Content.FirstOrDefault()?.Text);
        
        record.Status = PhoneOrderRecordStatus.Sent;
        record.TranscriptionText = completion.Content.FirstOrDefault()?.Text ?? "";

        var checkCustomerFriendly = await CheckCustomerFriendlyAsync(record.TranscriptionText, cancellationToken).ConfigureAwait(false);

        record.IsCustomerFriendly = checkCustomerFriendly.IsCustomerFriendly;
        record.IsHumanAnswered = checkCustomerFriendly.IsHumanAnswered;
        
        var detection = await _translationClient.DetectLanguageAsync(record.TranscriptionText, cancellationToken).ConfigureAwait(false);

        await MultiScenarioCustomProcessingAsync(agent, aiSpeechAssistant, record, cancellationToken).ConfigureAwait(false);
        
        var hasPendingTasks = await _salesDataProvider.HasPendingTasksByRecordIdAsync(record.Id, cancellationToken).ConfigureAwait(false);
        
        if (!hasPendingTasks)
        {
            Log.Information("No PhoneOrderPushTask created, mark record completed. RecordId={RecordId}", record.Id);

            await _phoneOrderDataProvider.MarkRecordCompletedAsync(record.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _backgroundJobClient.Enqueue<ISalesPhoneOrderPushService>(service => service.ExecutePhoneOrderPushTasksAsync(record.Id, CancellationToken.None));
        }

        if (agent.SourceSystem == AgentSourceSystem.Smarties)
            await CallBackSmartiesRecordAsync(agent, record, cancellationToken).ConfigureAwait(false);
        
        var reports = new List<PhoneOrderRecordReport>();

        reports.Add(new PhoneOrderRecordReport
        {
            RecordId = record.Id,
            Report = record.TranscriptionText,
            Language = SelectReportLanguageEnum(detection.Language),
            IsOrigin = SelectReportLanguageEnum(detection.Language) == record.Language,
            CreatedDate = DateTimeOffset.Now,
        });
        
        var targetLanguage = SelectReportLanguageEnum(detection.Language) == TranscriptionLanguage.Chinese ? "en" : "zh";
        
        var reportLanguage = SelectReportLanguageEnum(detection.Language) == TranscriptionLanguage.Chinese ? TranscriptionLanguage.English : TranscriptionLanguage.Chinese;
        
        var translatedText = await _translationClient.TranslateTextAsync(record.TranscriptionText, targetLanguage, cancellationToken: cancellationToken).ConfigureAwait(false);

        reports.Add(new PhoneOrderRecordReport
        {
            RecordId = record.Id,
            Report = translatedText.TranslatedText,
            Language = reportLanguage,
            IsOrigin = reportLanguage == record.Language,
            CreatedDate = DateTimeOffset.Now,
        });

        await _phoneOrderDataProvider.AddPhoneOrderRecordReportsAsync(reports, true, cancellationToken).ConfigureAwait(false);
        
        await CallBackSmartiesRecordAsync(agent, record, cancellationToken).ConfigureAwait(false);

        var message = agent.WechatRobotMessage?.Replace("#{assistant_name}", aiSpeechAssistant?.Name ?? "").Replace("#{agent_id}", agent.Id.ToString()).Replace("#{record_id}", record.Id.ToString()).Replace("#{assistant_file_url}", record.Url);

        message = await SwitchKeyMessageByGetUserProfileAsync(record, callFrom, aiSpeechAssistant, agent, message, cancellationToken).ConfigureAwait(false);

        await SendWorkWechatMessageByRobotKeyAsync(message, record, audioContent, agent, aiSpeechAssistant, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> SwitchKeyMessageByGetUserProfileAsync(PhoneOrderRecord record, string callFrom, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, Agent agent, string message, CancellationToken cancellationToken)
    {
        if (callFrom != null && aiSpeechAssistant?.Id != null && !string.IsNullOrEmpty(message))
        {
            var userProfile = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantUserProfileAsync(aiSpeechAssistant.Id, callFrom, cancellationToken).ConfigureAwait(false);
            var salesName = userProfile?.ProfileJson != null ? JObject.Parse(userProfile.ProfileJson).GetValue("correspond_sales")?.ToString() : string.Empty;
            
            var salesDisplayName = !string.IsNullOrEmpty(salesName) ? $"{salesName}" : "";

            message = message.Replace("#{sales_name}", salesDisplayName);
        }

        return message;
    }

    private async Task SendWorkWechatMessageByRobotKeyAsync(string message, PhoneOrderRecord record, byte[] audioContent, Agent agent, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(agent.WechatRobotKey) && !string.IsNullOrEmpty(message))
        {
            if (agent.IsWecomMessageOrder && aiSpeechAssistant != null)
            {
                var messageNumber = await SendAgentMessageRecordAsync(agent, record.Id, aiSpeechAssistant.GroupKey, cancellationToken);
                message = $"【第{messageNumber}條】\n" + message;
            }

            if (agent.IsSendAnalysisReportToWechat && !string.IsNullOrEmpty(record.TranscriptionText))
            {
                message += "\n\n" + record.TranscriptionText;
            }

            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(audioContent, agent.WechatRobotKey, message, Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CallBackSmartiesRecordAsync(Agent agent, PhoneOrderRecord record, CancellationToken cancellationToken = default)
    {
        if (agent.Type == AgentType.AiKid)
        {
            var aiKid = await _aiSpeechAssistantDataProvider.GetAiKidAsync(agentId: agent.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
            Log.Information("Get ai kid: {@Kid} by agentId: {AgentId}", aiKid, agent.Id);

            if (aiKid == null)throw new Exception($"Could not found ai kid by agentId: {agent.Id}");
        
            await _smartiesClient.CallBackSmartiesAiKidRecordAsync(new AiKidCallBackRequestDto
            {
                Url = record.Url,
                Uuid = aiKid.KidUuid,
                SessionId = record.SessionId
            }, cancellationToken).ConfigureAwait(false);
        }
        else
            await _smartiesClient.CallBackSmartiesAiSpeechAssistantRecordAsync(new AiSpeechAssistantCallBackRequestDto { CallSid = record.SessionId, RecordUrl = record.Url, RecordAnalyzeReport =  record.TranscriptionText }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> SendAgentMessageRecordAsync(Agent agent, int recordId, int groupKey, CancellationToken cancellationToken)
    {
        var timezone = !string.IsNullOrWhiteSpace(agent.Timezone) ? TimeZoneInfo.FindSystemTimeZoneById(agent.Timezone) : TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var nowDate = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timezone);

        var utcDate = TimeZoneInfo.ConvertTimeToUtc(nowDate.Date, timezone);

        var existingCount = await _aiSpeechAssistantDataProvider.GetMessageCountByAgentAndDateAsync(groupKey, utcDate, cancellationToken).ConfigureAwait(false);

        var messageNumber = existingCount + 1;

        var newRecord = new AgentMessageRecord
        {
            AgentId = agent.Id,
            GroupKey = groupKey,
            RecordId = recordId,
            MessageNumber = messageNumber
        };

        await _aiSpeechAssistantDataProvider.AddAgentMessageRecordAsync(newRecord, cancellationToken).ConfigureAwait(false);

        return messageNumber;
    }
    
    private List<SpeechMaticsSpeakInfoDto> StructureDiarizationResults(List<SpeechMaticsResultDto> results)
    {
        string currentSpeaker = null;
        PhoneOrderRole? currentRole = null;
        var startTime = 0.0;
        var endTime = 0.0;
        var speakInfos = new List<SpeechMaticsSpeakInfoDto>();

        foreach (var result in results.Where(result => !result.Alternatives.IsNullOrEmpty()))
        {
            if (currentSpeaker == null)
            {
                currentSpeaker = result.Alternatives[0].Speaker;
                currentRole = PhoneOrderRole.Restaurant;
                startTime = result.StartTime;
                endTime = result.EndTime;
                continue;
            }

            if (result.Alternatives[0].Speaker.Equals(currentSpeaker))
            {
                endTime = result.EndTime;
            }
            else
            {
                speakInfos.Add(new SpeechMaticsSpeakInfoDto { EndTime = endTime, StartTime = startTime, Speaker = currentSpeaker, Role = currentRole.Value });
                currentSpeaker = result.Alternatives[0].Speaker;
                currentRole = currentRole == PhoneOrderRole.Restaurant ? PhoneOrderRole.Client : PhoneOrderRole.Restaurant;
                startTime = result.StartTime;
                endTime = result.EndTime;
            }
        }

        speakInfos.Add(new SpeechMaticsSpeakInfoDto { EndTime = endTime, StartTime = startTime, Speaker = currentSpeaker });

        Log.Information("Structure diarization results : {@speakInfos}", speakInfos);
        
        return speakInfos;
    }
    
    private TranscriptionLanguage SelectReportLanguageEnum(string language)
    {
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return TranscriptionLanguage.Chinese;
    
        return TranscriptionLanguage.English;
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
    
     private async Task<List<ChatMessage>> ConfigureRecordAnalyzePromptAsync(Agent agent, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, string callFrom, string currentTime, byte[] audioContent, CancellationToken cancellationToken) 
    {
        var soldToIds = !string.IsNullOrEmpty(aiSpeechAssistant.Name) ? aiSpeechAssistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();

        var customerItemsCacheList = await _salesDataProvider.GetCustomerItemsCacheBySoldToIdsAsync(soldToIds, cancellationToken);
        var customerItemsString = string.Join(Environment.NewLine, soldToIds.Select(id => customerItemsCacheList.FirstOrDefault(c => c.Filter == id)?.CacheValue ?? ""));

        var audioData = BinaryData.FromBytes(audioContent);
        List<ChatMessage> messages =
        [
            new SystemChatMessage( (string.IsNullOrEmpty(aiSpeechAssistant?.CustomRecordAnalyzePrompt)
                ? "你是一名電話錄音的分析員，通過聽取錄音內容和語氣情緒作出精確分析，冩出一份分析報告。\n\n分析報告的格式：交談主題：xxx\n\n 來電號碼：#{call_from}\n\n 內容摘要:xxx \n\n 客人情感與情緒: xxx \n\n 待辦事件: \n1.xxx\n2.xxx \n\n 客人下單內容(如果沒有則忽略)：1. 牛肉(1箱)\n2. 雞腿肉(1箱)"
                : aiSpeechAssistant.CustomRecordAnalyzePrompt).Replace("#{call_from}", callFrom ?? "").Replace("#{current_time}", currentTime ?? "").Replace("#{customer_items}", customerItemsString ?? "")),
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage("幫我根據錄音生成分析報告：")
        ];

        return messages;
    }

    private async Task MultiScenarioCustomProcessingAsync(Agent agent, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, PhoneOrderRecord record, CancellationToken cancellationToken) 
    { 
        switch (agent.Type) 
        { 
            case AgentType.Sales: 
                if (!string.IsNullOrEmpty(record.TranscriptionText)) 
                { 
                    if (!aiSpeechAssistant.IsAllowOrderPush)
                    {
                        Log.Information("Assistant.Name={AssistantName} 的 is_allow_order_push=false，跳过生成草稿单", aiSpeechAssistant.Name);
                        
                        return;
                    }
                    
                    await HandleSalesScenarioAsync(agent, aiSpeechAssistant, record, cancellationToken).ConfigureAwait(false);
                }
                break; 
        } 
    }

    private async Task HandleSalesScenarioAsync(Agent agent, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, PhoneOrderRecord record, CancellationToken cancellationToken)
    { 
        if (string.IsNullOrEmpty(record.TranscriptionText)) return;

        var soldToIds = new List<string>(); 
        if (!string.IsNullOrEmpty(aiSpeechAssistant.Name))
             soldToIds = aiSpeechAssistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

        var historyItems = await GetCustomerHistoryItemsBySoldToIdAsync(soldToIds, cancellationToken).ConfigureAwait(false);

        var extractedOrders = await ExtractAndMatchOrderItemsFromReportAsync(record.TranscriptionText, historyItems, cancellationToken).ConfigureAwait(false); 
        if (!extractedOrders.Any()) return;

        var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var pacificNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pacificZone);

        foreach (var storeOrder in extractedOrders)
        { 
            var soldToId = await ResolveSoldToIdAsync(storeOrder, aiSpeechAssistant, soldToIds, cancellationToken).ConfigureAwait(false);
            
            await RefineOrderByAiAsync(storeOrder, soldToId, aiSpeechAssistant, historyItems, record.Id, cancellationToken).ConfigureAwait(false);

            if (storeOrder.IsDeleteWholeOrder && !storeOrder.Orders.Any())
            {
                await CreateDeleteOrderTaskAsync(record, storeOrder, soldToId, soldToIds, pacificZone, pacificNow, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var draftOrder = CreateDraftOrder(storeOrder, soldToId, aiSpeechAssistant, pacificZone, pacificNow, storeOrder.IsUndoCancel);

            await CreateGenerateOrderTaskAsync(record, storeOrder, draftOrder, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<List<ExtractedOrderDto>> ExtractAndMatchOrderItemsFromReportAsync(string reportText, List<(string Material, string MaterialDesc, DateTime? invoiceDate)> historyItems, CancellationToken cancellationToken) 
    { 
        var client = new ChatClient("gpt-4.1", _openAiSettings.ApiKey);

        var materialListText = string.Join("\n", historyItems.Select(x => $"{x.MaterialDesc} ({x.Material})【{x.invoiceDate}】"));

        var systemPrompt =
                "你是一名訂單分析助手。請從下面的客戶分析報告文字中提取所有下單的物料名稱、數量、單位，並且用歷史物料列表盡力匹配每個物料的materialNumber。" +
                "如果報告中提到了預約送貨時間，請提取送貨時間（格式yyyy-MM-dd）。" +
                "如果客戶提到了分店名，請提取 StoreName；如果提到第幾家店，請提取 StoreNumber。\n" +

                "【訂單意圖判斷規則（非常重要）】\n" +
                "1. 如果客戶明確表示取消整張訂單、全部不要、整單取消、今天的單都不要，請在該店鋪標記 IsDeleteWholeOrder=true，orders 可以為空陣列。\n" +
                "2. 如果客戶先說取消整單，後面又表示還是要、算了繼續下單、剛剛的取消不算，請標記 IsUndoCancel=true。\n" +
                "3. 如果客戶只取消單個物料（例如：某某不要了、某某取消、某某 cut 掉），請保留該物料，並在該物料上標記 markForDelete=true，有提到數量的話 quantity 需要用負數表示\n" +
                "4. 單個物料取消不等於取消整單，IsDeleteWholeOrder = false。\n" +
                "4. 如果得到的字段restored是true，就為true，是false或者沒有得到這個字段，則為false\n" +
                "5. 如果是減少某個物料的數量，請在該物料的 quantity 使用負數表示，並要使用 markForDelete = true。\n\n" +

                "請嚴格傳回一個 JSON 對象，頂層字段為 \"stores\"，每个店铺对象包含：" +
                "StoreName（可空字符串）, StoreNumber（可空字符串）, DeliveryDate（可空字符串）, " +
                "IsDeleteWholeOrder（boolean，默認 false）, IsUndoCancel（boolean，默認 false）, " +
                "orders（数组，元素包含 name, quantity, unit, materialNumber, markForDelete）。\n" +

                "範例：\n" +
                "{\n" +
                "  \"stores\": [\n" +
                "    {\n" +
                "      \"StoreName\": \"HaiDiLao\",\n" +
                "      \"StoreNumber\": \"1\",\n" +
                "      \"DeliveryDate\": \"2025-08-20\",\n" +
                "      \"IsDeleteWholeOrder\": false,\n" +
                "      \"IsUndoCancel\": false,\n" +
                "      \"orders\": [\n" +
                "        {\n" +
                "          \"name\": \"雞胸肉\",\n" +
                "          \"quantity\": 1,\n" +
                "          \"unit\": \"箱\",\n" +
                "          \"materialNumber\": \"000000000010010253\",\n" +
                "          \"markForDelete\": false,\n" +
                "          \"restored\": false\n" +
                "        }\n" +
                "      ]\n" +
                "    }\n" +
                "  ]\n" +
                "}\n" +

                "歷史物料列表：\n" + materialListText + "\n\n" +

                "每個物料的格式為「物料名稱（物料號碼）」，部分物料會包含日期。\n" +
                "當有多個相似的物料名稱時，請根據以下規則選擇匹配的物料號碼：\n" +
                "1. **優先選擇沒有日期的物料。**\n" +
                "2. 如果所有相似物料都有日期，請選擇日期 **最新** 的那個物料。\n\n" +

                "注意：\n" +
                "1. 必須嚴格輸出 JSON，物件頂層字段必須是 \"stores\"，不要有其他字段或額外說明。\n" +
                "2. 提取的物料名稱需要為繁體中文。\n" +
                "3. 如果沒有提到店鋪信息，但有下單內容，StoreName 和 StoreNumber 可為空值，orders 要正常提取。\n" +
                "4. **如果客戶分析文本中沒有任何可識別的下單信息，請返回：{ \"stores\": [] }。不得臆造或猜測物料。**\n" +
                "5. 請務必完整提取報告中每一個提到的物料，如果你不知道它的materialNumber，那也必須保留該物料的quantity以及name。";
        Log.Information("Sending prompt to GPT: {Prompt}", systemPrompt);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage("客戶分析報告文本：\n" + reportText + "\n\n")
        };

        var completion = await client.CompleteChatAsync(messages, new ChatCompletionOptions { ResponseModalities = ChatResponseModalities.Text, ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() }, cancellationToken).ConfigureAwait(false);
        var jsonResponse = completion.Value.Content.FirstOrDefault()?.Text ?? "";
        
        Log.Information("AI JSON Response: {JsonResponse}", jsonResponse);

        try 
        { 
            using var jsonDoc = JsonDocument.Parse(jsonResponse);

            var storesArray = jsonDoc.RootElement.GetProperty("stores");
            var results = new List<ExtractedOrderDto>();

            foreach (var storeElement in storesArray.EnumerateArray())
            {
                var storeDto = new ExtractedOrderDto
                {
                    StoreName = storeElement.TryGetProperty("StoreName", out var sn) ? sn.GetString() ?? "" : "",
                    StoreNumber = storeElement.TryGetProperty("StoreNumber", out var snum) ? snum.GetString() ?? "" : "",
                    DeliveryDate = storeElement.TryGetProperty("DeliveryDate", out var dd) && DateTime.TryParse(dd.GetString(), out var dt) ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(1),
                    IsDeleteWholeOrder = storeElement.TryGetProperty("IsDeleteWholeOrder", out var del) && del.GetBoolean(),
                    IsUndoCancel = storeElement.TryGetProperty("IsUndoCancel", out var undo) && undo.GetBoolean()
                }; 

                if (storeElement.TryGetProperty("orders", out var ordersArray)) 
                { 
                    foreach (var orderItem in ordersArray.EnumerateArray()) 
                    { 
                        var name = orderItem.TryGetProperty("name", out var n) ? n.GetString() ?? "" : ""; 
                        var qty = orderItem.TryGetProperty("quantity", out var q) && q.TryGetDecimal(out var dec) ? dec : 0; 
                        var unit = orderItem.TryGetProperty("unit", out var u) ? u.GetString() ?? "" : ""; 
                        var materialNumber = orderItem.TryGetProperty("materialNumber", out var mn) ? mn.GetString() ?? "" : ""; 
                        var markForDelete = orderItem.TryGetProperty("markForDelete", out var md) && md.GetBoolean();
                        var restored = orderItem.TryGetProperty("restored", out var rd) && rd.GetBoolean();

                        if (string.IsNullOrWhiteSpace(materialNumber))
                            materialNumber = MatchMaterialNumber(name, materialNumber, unit, historyItems);
                        
                        storeDto.Orders.Add(new ExtractedOrderItemDto
                        {
                            Unit = unit,
                            Name = name,
                            Quantity = (int)qty,
                            MarkForDelete = markForDelete,
                            MaterialNumber = materialNumber,
                            Restored = restored
                        });
                    } 
                }

                results.Add(storeDto); 
            }

            return results;
        }
        catch (Exception ex) 
        { 
            Log.Warning("解析GPT返回JSON失败: {Message}", ex.Message);
            return new List<ExtractedOrderDto>();
        } 
    }
    
    private async Task<List<(string Material, string MaterialDesc, DateTime? InvoiceDate)>> GetCustomerHistoryItemsBySoldToIdAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        List<(string Material, string MaterialDesc, DateTime? InvoiceDate)> historyItems = new List<(string, string, DateTime?)>();

        var askInfoResponse = await _salesClient.GetAskInfoDetailListByCustomerAsync(new GetAskInfoDetailListByCustomerRequestDto { CustomerNumbers = soldToIds }, cancellationToken).ConfigureAwait(false);
        var orderHistoryResponse = await _salesClient.GetOrderHistoryByCustomerAsync(new GetOrderHistoryByCustomerRequestDto { CustomerNumber = soldToIds.FirstOrDefault() }, cancellationToken).ConfigureAwait(false);

        if (askInfoResponse?.Data != null && askInfoResponse.Data.Any())
            historyItems.AddRange(askInfoResponse.Data.Where(x => !string.IsNullOrWhiteSpace(x.Material)).Select(x => (x.Material, x.MaterialDesc, (DateTime?)null)));

        if (orderHistoryResponse?.Data != null && orderHistoryResponse.Data.Any())
            historyItems.AddRange(orderHistoryResponse?.Data.Where(x => !string.IsNullOrWhiteSpace(x.MaterialNumber)).Select(x => (x.MaterialNumber, x.MaterialDescription, x.LastInvoiceDate)) ?? new List<(string, string, DateTime?)>());

        return historyItems;
    }

    private string MatchMaterialNumber(string itemName, string baseNumber, string unit, List<(string Material, string MaterialDesc, DateTime? invoiceDate)> historyItems)
    {
        var candidates = historyItems.Where(x => x.MaterialDesc != null && x.MaterialDesc.Contains(itemName, StringComparison.OrdinalIgnoreCase)).Select(x => x.Material).ToList();
        Log.Information("Candidate material code list: {@Candidates}", candidates);

        if (!candidates.Any()) return string.IsNullOrEmpty(baseNumber) ? "" : baseNumber;
        if (candidates.Count == 1) return candidates.First();

        var isCase = !string.IsNullOrWhiteSpace(unit) && (unit.Contains("case", StringComparison.OrdinalIgnoreCase) || unit.Contains("箱"));
        if (isCase)
        {
            var noPcList = candidates.Where(x => !x.Contains("PC", StringComparison.OrdinalIgnoreCase)).ToList();

            if (noPcList.Any())
                return noPcList.First(); 
            
            return candidates.First();
        }
        
        var pcList = candidates.Where(x => x.Contains("PC", StringComparison.OrdinalIgnoreCase)).ToList();

        if (pcList.Any())
            return pcList.First();
        
        return candidates.First();
    }

    private async Task<string> ResolveSoldToIdAsync(ExtractedOrderDto storeOrder, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, List<string> soldToIds, CancellationToken cancellationToken) 
    { 
        if (soldToIds.Count == 1)
            return soldToIds[0];
        
        if (!string.IsNullOrEmpty(storeOrder.StoreName)) 
        { 
            var requestDto = new GetCustomerNumbersByNameRequestDto { CustomerName = storeOrder.StoreName }; 
            var customerNumber = await _salesClient.GetCustomerNumbersByNameAsync(requestDto, cancellationToken).ConfigureAwait(false); 
            return customerNumber?.Data?.FirstOrDefault()?.CustomerNumber ?? string.Empty; 
        }

        if (!string.IsNullOrEmpty(storeOrder.StoreNumber) && soldToIds.Any() && int.TryParse(storeOrder.StoreNumber, out var storeIndex) && storeIndex > 0 && storeIndex <= soldToIds.Count)
        {
            return soldToIds[storeIndex - 1];
        }

        if (soldToIds.Count > 1) return string.Empty;

        return aiSpeechAssistant.Name; 
    }

    private GenerateAiOrdersRequestDto CreateDraftOrder(ExtractedOrderDto storeOrder, string soldToId, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, TimeZoneInfo pacificZone, DateTime pacificNow, bool useCanceledOrder) 
    { 
        var pacificDeliveryDate = storeOrder.DeliveryDate != default ? TimeZoneInfo.ConvertTimeFromUtc(storeOrder.DeliveryDate, pacificZone) : pacificNow.AddDays(1);

        var assistantNameWithComma = aiSpeechAssistant.Name?.Replace('/', ',') ?? string.Empty;

        return new GenerateAiOrdersRequestDto
        {
            AiModel = "Smartalk",
            UseCanceledOrder = useCanceledOrder,
            AiOrderInfoDto = new AiOrderInfoDto
            {
                SoldToId = soldToId,
                AiAssistantId = aiSpeechAssistant.Id,
                SoldToIds = string.IsNullOrEmpty(soldToId) ? assistantNameWithComma : soldToId,
                DocumentDate = pacificNow.Date,
                DeliveryDate = pacificDeliveryDate.Date,
                AiOrderItemDtoList = storeOrder.Orders.Select(i => new AiOrderItemDto
                {
                    MaterialNumber = i.MaterialNumber,
                    AiMaterialDesc = i.Name,
                    MaterialQuantity = i.Quantity,
                    AiUnit = i.Unit,
                    MarkForDelete = i.MarkForDelete,
                    Restored = i.Restored
                }).ToList()
            }
        };
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
    
    private async Task CreateDeleteOrderTaskAsync(PhoneOrderRecord record, ExtractedOrderDto storeOrder, string soldToId, List<string> soldToIds, TimeZoneInfo pacificZone, DateTime pacificNow, CancellationToken cancellationToken)
    {
        var pacificDeliveryDate = storeOrder.DeliveryDate != default ? TimeZoneInfo.ConvertTimeFromUtc(storeOrder.DeliveryDate, pacificZone) : pacificNow.AddDays(1);
        var req = new DeleteAiOrderRequestDto
        {
            CustomerNumber = soldToId,
            SoldToIds = string.Join(",", soldToIds),
            DeliveryDate = pacificDeliveryDate.Date,
            AiAssistantId = record.AssistantId ?? 0
        };
        
        var task = new PhoneOrderPushTask
        {
            RecordId = record.Id,
            ParentRecordId = record.ParentRecordId,
            AssistantId = record.AssistantId ?? 0,
            TaskType = PhoneOrderPushTaskType.DeleteOrder,
            BusinessKey = $"DELETE_{storeOrder.StoreName}_{storeOrder.DeliveryDate:yyyyMMdd}",
            RequestJson = JsonSerializer.Serialize(req),
            Status = PhoneOrderPushTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _salesDataProvider.AddPhoneOrderPushTaskAsync(task, true, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task CreateGenerateOrderTaskAsync(PhoneOrderRecord record, ExtractedOrderDto storeOrder, GenerateAiOrdersRequestDto request, CancellationToken cancellationToken)
    {
        var task = new PhoneOrderPushTask
        {
            RecordId = record.Id,
            ParentRecordId = record.ParentRecordId,
            AssistantId = record.AssistantId ?? 0,
            TaskType = PhoneOrderPushTaskType.GenerateOrder,
            BusinessKey = $"{storeOrder.StoreName}_{storeOrder.DeliveryDate:yyyyMMdd}",
            RequestJson = JsonSerializer.Serialize(request),
            Status = PhoneOrderPushTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _salesDataProvider.AddPhoneOrderPushTaskAsync(task, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task RefineOrderByAiAsync(ExtractedOrderDto storeOrder, string soldToId, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant,  List<(string Material, string MaterialDesc, DateTime? invoiceDate)> historyItems, int recordId, CancellationToken cancellationToken)
    {
        var draftOrder = await _salesClient.GetAiOrderItemsByDeliveryDateAsync(new GetAiOrderItemsByDeliveryDateRequestDto { CustomerNumber = soldToId, DeliveryDate = storeOrder.DeliveryDate }, cancellationToken).ConfigureAwait(false);
        
        var todayReports = await GetTodayReportsByAssistantAsync(aiSpeechAssistant.Id, recordId, cancellationToken).ConfigureAwait(false);
        
        var hasDraftOrder = draftOrder?.Data != null && draftOrder.Data.Any();
        var hasTodayReports = todayReports != null && todayReports.Any();

        if (!hasDraftOrder && !hasTodayReports)
        {
            Log.Information("Skip RefineOrderByAiAsync: no draft order and no today reports. SoldToId={SoldToId}, DeliveryDate={DeliveryDate}", soldToId, storeOrder.DeliveryDate);
            return;
        }
        
        var client = new ChatClient("gpt-4.1", _openAiSettings.ApiKey);

        var currentOrdersJson = JsonSerializer.Serialize(storeOrder.Orders);
        
        var draftOrderJson = draftOrder?.Data != null ? JsonSerializer.Serialize(draftOrder.Data) : "[]";
        
        var historyReportsText = todayReports.Any() ? string.Join("\n---\n", todayReports) : "（无）";

        var systemPrompt =
            "你是一名「电话下单意图裁决助手」，负责在多通电话、多次加单、减单、取消、撤销取消的情况下，" +
            "判断客户『最终明确要执行』的订单结果。\n\n" +
            "你将同时获得三类信息：\n" +
            "1. 本次通话中提取出的订单内容（优先级最高）\n" +
            "2. 系统中已存在的草稿单内容\n" +
            "3. 今天该客户的历史分析报告（按时间顺序）\n\n" +
            "【裁决优先级规则（非常重要）】\n" +
            "- 本次通话内容优先级最高\n" +
            "- 历史分析报告仅作为上下文参考，不可覆盖本次通话中的明确指令\n" +
            "- 草稿单仅用于判断是否存在、是否被取消，不可覆盖本次通话指令\n\n" +
            "【你的核心任务】\n" +
            "1. 判断是否为整单取消（IsDeleteWholeOrder）\n" +
            "2. 判断是否为撤销之前的取消（IsUndoCancel）\n" +
            "3. 合并同一物料的多次加减单\n" +
            "4. 若加减数量相互抵消，只输出最终数量\n" +
            "5. 输出客户最终明确要执行的订单结果\n\n" +
            "【整单取消规则】\n" +
            "- 只有当客户在『本次通话中』明确表示“整单取消 / 全部不要 / 今天的单都取消”等语义，" +
            "才能将 IsDeleteWholeOrder 设置为 true\n" +
            "- 整单取消时，Orders 必须为空数组\n" +
            "- 仅取消单个物料，不等于整单取消\n\n" +
            "【撤销取消规则】\n" +
            "- 当客户在本次通话中明确表示“刚刚取消的不算 / 还是要 / 恢复之前的订单”，" +
            "才能将 IsUndoCancel 设置为 true\n\n" +
            "【物料合并规则】\n" +
            "- 同一物料必须合并为一条\n" +
            "- 如果物料在多通电话中被多次加单或减单，必须计算最终净数量\n" +
            "- 不允许输出重复物料\n\n" +
            "【物料名称拼接规则（必须遵守）】\n" +
            "- Name 字段必须体现加减过程\n" +
            "- 拼接格式：物料名#第一次数量单位+后续变更\n" +
            "- 示例：\n" +
            "  第一通：鸡胸肉 1 箱\n" +
            "  第二通：加 2 箱\n" +
            "  最终输出：\n" +
            "  Name = 鸡胸肉#1箱+2\n" +
            "  Quantity = 3\n" +
            "  Unit = 箱\n\n" +
            "【单个物料取消规则（非常重要）】\n" +
            "- 如果客户在本次通话中明确表示取消某个具体物料，" +
            "即使该物料在当前草稿单中已经不存在，也必须输出该物料\n" +
            "- 此时必须设置 MarkForDelete = true\n" +
            "- 仅仅“没有再提到”某个物料，不等于取消，禁止设置 MarkForDelete\n\n" +
            "【恢复已取消物料规则】\n" +
            "- 当客户明确表示恢复、继续要、撤销之前的取消，" +
            "且该物料在历史中曾被取消，必须设置：\n" +
            "  MarkForDelete = false\n\n" +
            "如果得到的字段restored是true，那就是Restore = true，如果是false或者沒有得到這個字段，則為 Restore = false\n" +
            "【禁止行为】\n" +
            "- 不得臆造任何通话中未提及的物料\n" +
            "- 不得因为草稿单中不存在就忽略明确的取消指令\n" +
            "- 不得把“未提及”当成“取消”\n" +
            "- 不得输出非 JSON 内容\n\n" +
            "【输出格式（必须严格是 JSON，不允许多字段或少字段）】\n" +
            "{\n" +
            "  \"StoreName\": \"\",\n" +
            "  \"StoreNumber\": \"\",\n" +
            "  \"DeliveryDate\": \"yyyy-MM-dd\",\n" +
            "  \"IsDeleteWholeOrder\": false,\n" +
            "  \"IsUndoCancel\": false,\n" +
            "  \"Orders\": [\n" +
            "    {\n" +
            "      \"Name\": \"\",\n" +
            "      \"Quantity\": 0,\n" +
            "      \"MaterialNumber\": \"\",\n" +
            "      \"Unit\": \"\",\n" +
            "      \"MarkForDelete\": false,\n" +
            "      \"Restored\": false\n" +
            "    }\n" +
            "  ]\n" +
            "}\n\n" +
            "如果最终没有任何有效下单内容，请返回 Orders 为空数组。";
        
        var userPrompt = "【本次通话提取的订单】\n" + currentOrdersJson + "\n\n" + "【系统中已有草稿单】\n" + draftOrderJson + "\n\n" + "【今日历史分析报告】\n" + historyReportsText + "\n";
        Log.Information("Sending refine prompt to GPT: {Prompt}", systemPrompt);
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var completion = await client.CompleteChatAsync(messages, new ChatCompletionOptions { ResponseModalities = ChatResponseModalities.Text, ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() }, cancellationToken).ConfigureAwait(false);
        
        var jsonResponse = completion.Value.Content.FirstOrDefault()?.Text ?? "";
        Log.Information("Second AI refine response: {Json}", jsonResponse);
        
        try
        {
            using var jsonDoc = JsonDocument.Parse(jsonResponse);
            var root = jsonDoc.RootElement;

            storeOrder.StoreName = root.TryGetProperty("StoreName", out var sn) ? sn.GetString() ?? "" : storeOrder.StoreName;

            storeOrder.StoreNumber = root.TryGetProperty("StoreNumber", out var storeNum) ? storeNum.GetString() ?? "" : storeOrder.StoreNumber;

            if (root.TryGetProperty("DeliveryDate", out var dd) && DateTime.TryParse(dd.GetString(), out var dt))
                storeOrder.DeliveryDate = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            storeOrder.IsDeleteWholeOrder = root.TryGetProperty("IsDeleteWholeOrder", out var del) && del.GetBoolean();

            storeOrder.IsUndoCancel = root.TryGetProperty("IsUndoCancel", out var undo) && undo.GetBoolean();

            storeOrder.Orders.Clear();

            if (root.TryGetProperty("Orders", out var ordersArray))
            {
                foreach (var orderItem in ordersArray.EnumerateArray())
                {
                    var name = orderItem.GetProperty("Name").GetString() ?? "";
                    var qty = orderItem.GetProperty("Quantity").GetInt32();
                    var unit = orderItem.GetProperty("Unit").GetString() ?? "";
                    var materialNumber = orderItem.GetProperty("MaterialNumber").GetString() ?? "";
                    var markForDelete = orderItem.GetProperty("MarkForDelete").GetBoolean();
                    var restored = orderItem.GetProperty("Restored").GetBoolean();

                    storeOrder.Orders.Add(new ExtractedOrderItemDto
                    {
                        Name = name,
                        Quantity = qty,
                        Unit = unit,
                        MaterialNumber = materialNumber,
                        MarkForDelete = markForDelete,
                        Restored = restored
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning("第二次模型解析失败，保留第一次模型结果: {Message}", ex.Message);
        }
    }
    
    private async Task<List<string>> GetTodayReportsByAssistantAsync(int assistantId, int recordId, CancellationToken cancellationToken)
    {
        var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var pacificNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, pacificZone);
        
        var pacificStartOfDay = new DateTimeOffset(pacificNow.Year, pacificNow.Month, pacificNow.Day, 0, 0, 0, pacificZone.GetUtcOffset(pacificNow));
        
        var utcStart = pacificStartOfDay.ToUniversalTime();
        var utcEnd = utcStart.AddDays(1);
        
        var reports = await _phoneOrderDataProvider.GetTranscriptionTextsAsync(assistantId, recordId, utcStart, utcEnd, cancellationToken).ConfigureAwait(false);
        return reports;
    }
}