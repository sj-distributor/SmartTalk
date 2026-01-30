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
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Pos;
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
    private readonly IPosService _posService;
    private readonly ISalesClient _salesClient;
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IPosUtilService _posUtilService;
    private readonly IPosDataProvider _posDataProvider;
    private readonly TranslationClient _translationClient;
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
        IPosService posService,
        ISalesClient salesClient,
        OpenAiSettings openAiSettings,
        TwilioSettings twilioSettings,
        IPosUtilService posUtilService,
        ISmartiesClient smartiesClient,
        IPosDataProvider posDataProvider,
        TranslationClient translationClient,
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
        _posService = posService;
        _salesClient = salesClient;
        _openAiSettings = openAiSettings;
        _twilioSettings = twilioSettings;
        _smartiesClient = smartiesClient;
        _posUtilService = posUtilService;
        _posDataProvider = posDataProvider;
        _translationClient = translationClient;
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
            
            await SummarizeConversationContentAsync(record, audioContent, cancellationToken).ConfigureAwait(false);
            
            await _phoneOrderService.ExtractPhoneOrderRecordAiMenuAsync(speakInfos, record, audioContent, cancellationToken).ConfigureAwait(false);
            
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
        var callSubjectCn = "通话主题:";
        var callSubjectEn = "Conversation topic:";

        var messages = await ConfigureRecordAnalyzePromptAsync(agent, aiSpeechAssistant, record, callFrom ?? "", callTo ?? "", currentTime, audioContent, callSubjectCn, callSubjectEn, cancellationToken);
        
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);
 
        ChatCompletionOptions options = new() { ResponseModalities = ChatResponseModalities.Text };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        Log.Information("sales record analyze report:" + completion.Content.FirstOrDefault()?.Text);
      
        record.Status = PhoneOrderRecordStatus.Sent;
        record.TranscriptionText = completion.Content.FirstOrDefault()?.Text ?? "";

        var checkCustomerFriendly = await CheckCustomerFriendlyAsync(record.TranscriptionText, cancellationToken).ConfigureAwait(false);

        record.IsHumanAnswered = checkCustomerFriendly.IsHumanAnswered;
        record.IsCustomerFriendly = checkCustomerFriendly.IsCustomerFriendly;

        var scenarioInformation = await IdentifyDialogueScenariosAsync(record.TranscriptionText, cancellationToken).ConfigureAwait(false);
        record.Scenario = scenarioInformation.Category;
        record.Remark = scenarioInformation.Remark;

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
        
        await _posUtilService.GenerateAiDraftAsync(agent, aiSpeechAssistant, record, cancellationToken).ConfigureAwait(false);
        
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
    
     private async Task<List<ChatMessage>> ConfigureRecordAnalyzePromptAsync(
         Agent agent, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, PhoneOrderRecord record, string callFrom,
         string callTo, string currentTime, byte[] audioContent, string callSubjectCn, string callSubjectEn, CancellationToken cancellationToken) 
    {
        var soldToIds = !string.IsNullOrEmpty(aiSpeechAssistant.Name) ? aiSpeechAssistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();

        var customerItemsCacheList = await _salesDataProvider.GetCustomerItemsCacheBySoldToIdsAsync(soldToIds, cancellationToken);
        var customerItemsString = string.Join(Environment.NewLine, soldToIds.Select(id => customerItemsCacheList.FirstOrDefault(c => c.Filter == id)?.CacheValue ?? ""));
        
        var (_, menuItems) = await _posUtilService.GeneratePosMenuItemsAsync(agent.Id, false, record.Language, cancellationToken).ConfigureAwait(false);

        var audioData = BinaryData.FromBytes(audioContent);
        List<ChatMessage> messages =
        [
            new SystemChatMessage( (string.IsNullOrEmpty(aiSpeechAssistant?.CustomRecordAnalyzePrompt)
                ? "你是一名電話錄音的分析員，通過聽取錄音內容和語氣情緒作出精確分析，冩出一份分析報告。\n\n" +
                  "分析報告的格式如下：" +
                  "交談主題：xxx\n\n " +
                  "來電號碼：#{call_from}\n\n " +
                  "內容摘要:xxx \n\n " +
                  "客人情感與情緒: xxx \n\n " +
                  "待辦事件: \n1.xxx\n2.xxx \n\n " +
                  "客人下單內容(如果沒有則忽略)：1. 牛肉(1箱)\n2. 雞腿肉(1箱)"
                : aiSpeechAssistant.CustomRecordAnalyzePrompt)
                .Replace("#{call_from}", callFrom ?? "")
                .Replace("#{current_time}", currentTime ?? "")
                .Replace("#{call_to}", callTo ?? "")
                .Replace("#{customer_items}", customerItemsString ?? "")
                .Replace("#{call_subject_cn}", callSubjectCn)
                .Replace("#{call_subject_us}", callSubjectEn)
                .Replace("#{menu_items}", menuItems ?? "")),
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

            if (storeOrder.IsDeleteWholeOrder && !storeOrder.Orders.Any())
            {
                await CreateDeleteOrderTaskAsync(record, storeOrder, soldToId, soldToIds, pacificZone, pacificNow, cancellationToken).ConfigureAwait(false);
                continue;
            }

            foreach (var item in storeOrder.Orders)
            {
                item.MaterialNumber = MatchMaterialNumber(item.Name, item.MaterialNumber, item.Unit, historyItems);
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
                "          \"markForDelete\": false\n" +
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

                        materialNumber = MatchMaterialNumber(name, materialNumber, unit, historyItems);

                        storeDto.Orders.Add(new ExtractedOrderItemDto
                        {
                            Unit = unit,
                            Name = name,
                            Quantity = (int)qty,
                            MarkForDelete = markForDelete,
                            MaterialNumber = materialNumber
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
                    MarkForDelete = i.MarkForDelete
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
                            " \"remark\": \"仅当category为'Other'时填写简短关键词（如‘咨询加盟’），其余类别留空\"\n}" +
                            "当一个对话中有多个场景出现时，需要严格遵循以下的识别优先级：" +
                            "*1.Order > 2.Reservation/InformationNotification > 3.Inquiry > 4.ComplaintFeedback > 5.TransferToHuman > 6.TransferVoicemail > 7.ThirdPartyOrderNotification > 8.SalesCall > 9.InvalidCall > 10.Other*"
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