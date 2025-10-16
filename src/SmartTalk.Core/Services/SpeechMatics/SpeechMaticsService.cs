using Twilio;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoMapper;
using Google.Cloud.Translation.V2;
using Serilog;
using SmartTalk.Core.Ioc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using Twilio.Rest.Api.V2010.Account;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.PhoneOrder;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.Sales;
using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Core.Services.SpeechMatics;

public interface ISpeechMaticsService : IScopedDependency
{
    Task HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken);

    Task<string> BuildCustomerItemsStringAsync(List<string> soldToIds, CancellationToken cancellationToken);
}

public class SpeechMaticsService : ISpeechMaticsService
{
    private readonly IMapper _mapper;
    private readonly ISalesClient _salesClient;
    private readonly IWeChatClient _weChatClient;
    private readonly IFfmpegService _ffmpegService;
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly TranslationClient _translationClient;
    private readonly ISmartiesClient _smartiesClient;
    private readonly PhoneOrderSetting _phoneOrderSetting;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
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
        IPhoneOrderDataProvider phoneOrderDataProvider,
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
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public async Task HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken)
    {
        if (command.Transcription == null || command.Transcription.Results.IsNullOrEmpty() || command.Transcription.Job == null || command.Transcription.Job.Id.IsNullOrEmpty()) return;

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

    public async Task<string> BuildCustomerItemsStringAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        var allItems = new List<string>();
        
        var askInfoResponse = await _salesClient.GetAskInfoDetailListByCustomerAsync(new GetAskInfoDetailListByCustomerRequestDto { CustomerNumbers = soldToIds }, cancellationToken).ConfigureAwait(false);
        var askItems = askInfoResponse?.Data ?? new List<VwAskDetail>();
        
        var orderItems = new List<SalesOrderHistoryDto>();
        if (soldToIds?.Any() == true)
        {
            var tasks = soldToIds.Select(async soldToId =>
            {
                var response = await _salesClient.GetOrderHistoryByCustomerAsync(new GetOrderHistoryByCustomerRequestDto { CustomerNumber = soldToId }, cancellationToken);
                return response?.Data ?? new List<SalesOrderHistoryDto>();
            });

            var results = await Task.WhenAll(tasks);
            orderItems = results.SelectMany(r => r).ToList();
        }
        
        var levelCodes = askItems.Where(x => !string.IsNullOrEmpty(x.LevelCode)).Select(x => x.LevelCode)
            .Concat(orderItems.Where(x => !string.IsNullOrEmpty(x.LevelCode)).Select(x => x.LevelCode)).Distinct().ToList();
        
        var habitResponse = levelCodes.Any() ? await _salesClient.GetCustomerLevel5HabitAsync(new GetCustomerLevel5HabitRequstDto { CustomerId = soldToIds.FirstOrDefault(), LevelCode5List = levelCodes }, cancellationToken).ConfigureAwait(false) : null;
        var habitLookup = habitResponse?.HistoryCustomerLevel5HabitDtos?.ToDictionary(h => h.LevelCode5, h => h) ?? new Dictionary<string, HistoryCustomerLevel5HabitDto>();
        
        string FormatItem(string materialDesc, string levelCode = null)
        {
            var parts = materialDesc?.Split('·') ?? Array.Empty<string>();
            var name = parts.Length > 4 ? $"{parts[0]}{parts[4]}" : parts.FirstOrDefault() ?? "";
            var brand = parts.Length > 1 ? parts[1] : "";
            var size = parts.Length > 3 ? parts[3] : "";
            
            string aliasText = "";
            MaterialPartInfoDto partInfo = null;
            if (!string.IsNullOrEmpty(levelCode) && habitLookup.TryGetValue(levelCode, out var habit))
            {
                aliasText = habit.CustomerLikeName ?? "";
                partInfo = habit.MaterialPartInfoDtos?.FirstOrDefault();
            }

            return $"Item: {name}, Brand: {brand}, Size: {size}, Aliases: {aliasText}, " +
                   $"baseUnit: {partInfo?.BaseUnit ?? ""}, salesUnit: {partInfo?.SalesUnit ?? ""}, weights: {partInfo?.Weights ?? 0}, " +
                   $"placeOfOrigin: {partInfo?.PlaceOfOrigin ?? ""}, packing: {partInfo?.Packing ?? ""}, specifications: {partInfo?.Specifications ?? ""}, " +
                   $"ranks: {partInfo?.Ranks ?? ""}, atr: {partInfo?.Atr ?? 0}";
        }
        
        allItems.AddRange(askItems.Select(x => FormatItem(x.MaterialDesc, x.LevelCode)));
        allItems.AddRange(orderItems.Select(x => FormatItem(x.MaterialDescription, x.LevelCode)));

        return string.Join(Environment.NewLine, allItems.Distinct());
    }

    private async Task SummarizeConversationContentAsync(PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken)
    {
        var (aiSpeechAssistant, agent) = await _aiSpeechAssistantDataProvider.GetAgentAndAiSpeechAssistantAsync(record.AgentId, cancellationToken).ConfigureAwait(false);

        Log.Information("Get Assistant: {@Assistant} and Agent: {@Agent} by agent id {agentId}", aiSpeechAssistant, agent, record.AgentId);
        
        var callFrom = string.Empty;
        try
        {
            TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

            var call = await CallResource.FetchAsync(record.SessionId);
            callFrom = call?.From;
            
            Log.Information("Fetched incoming phone number from Twilio: {callFrom}", callFrom);
        }
        catch (Exception e)
        {
            Log.Warning("Fetched incoming phone number from Twilio failed: {Message}", e.Message);
        }

        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");
        
        var messages = await ConfigureRecordAnalyzePromptAsync(agent, aiSpeechAssistant, callFrom ?? "", currentTime, audioContent, cancellationToken);
        
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);
        
        ChatCompletionOptions options = new() { ResponseModalities = ChatResponseModalities.Text, MaxOutputTokenCount = 16384 };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        Log.Information("sales record analyze report:" + completion.Content.FirstOrDefault()?.Text);
        
        record.Status = PhoneOrderRecordStatus.Sent;
        record.TranscriptionText = completion.Content.FirstOrDefault()?.Text ?? "";
    
        var isCustomerFriendly = await CheckCustomerFriendlyAsync(record.TranscriptionText, cancellationToken).ConfigureAwait(false);
        
        var detection = await _translationClient.DetectLanguageAsync(record.TranscriptionText, cancellationToken).ConfigureAwait(false);

        var reports = new List<PhoneOrderRecordReport>();

        reports.Add(new PhoneOrderRecordReport
        {
            RecordId = record.Id,
            Report = record.TranscriptionText,
            Language = SelectReportLanguageEnum(detection.Language),
            IsOrigin = SelectReportLanguageEnum(detection.Language) == record.Language,
            CreatedDate = DateTimeOffset.Now,
            IsCustomerFriendly = isCustomerFriendly
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
            IsCustomerFriendly = isCustomerFriendly
        });

        await _phoneOrderDataProvider.AddPhoneOrderRecordReportsAsync(reports, true, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Handle Smarties callback if required: {@Agent}、{@Record}", agent, record);

        await MultiScenarioCustomProcessingAsync(agent, aiSpeechAssistant, record, cancellationToken).ConfigureAwait(false);
        
        await CallBackSmartiesRecordAsync(agent, record, cancellationToken).ConfigureAwait(false);

        if (agent.SourceSystem == AgentSourceSystem.Smarties) 
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
                var messageNumber = await SendAgentMessageRecordAsync(agent, record.Id, aiSpeechAssistant.GroupKey, cancellationToken).ConfigureAwait(false);
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
        Log.Information("CallBackSmartiesRecordAsync: {@Agent}、{@Record}", agent, record);
        
        if (agent.Type == AgentType.AiKid)
        {
            Log.Information("Ready send ai kid record !!!");
            
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
    
    private async Task<List<ChatMessage>> ConfigureRecordAnalyzePromptAsync(Agent agent, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, string callFrom, string currentTime, byte[] audioContent, CancellationToken cancellationToken) 
    {
        var askItemsJson = string.Empty;
        
        if (agent.Type == AgentType.Sales)
        {
            var sales = await _aiSpeechAssistantDataProvider.GetCallInSalesByNameAsync(aiSpeechAssistant.Name, SalesCallType.CallIn, cancellationToken).ConfigureAwait(false);
            Log.Information("Sales fetch result: {@Sales}", sales);

            if (sales != null)
            {
                var requestDto = new GetAskInfoDetailListByCustomerRequestDto
                {
                    CustomerNumbers = new List<string> { aiSpeechAssistant.Name }
                };

                var askedItems = await _salesClient.GetAskInfoDetailListByCustomerAsync(requestDto, cancellationToken).ConfigureAwait(false);
                
                if (askedItems?.Data == null || !askedItems.Data.Any())
                {
                    Log.Warning("Sales API 返回空数据，客户：{Customer}", aiSpeechAssistant.Name);
                    
                    askedItems = new GetAskInfoDetailListByCustomerResponseDto { Data = new List<VwAskDetail>() };
                }

                var topItems = askedItems.Data.OrderByDescending(x => x.ValidAskQty).Take(60).ToList();
                
                var simplifiedItems = topItems.Select(x => new
                {
                    name = x.MaterialDesc,
                    quantity = x.ValidAskQty,
                    materialNumber = x.Material
                }).ToList();

                askItemsJson = JsonSerializer.Serialize(simplifiedItems, new JsonSerializerOptions { WriteIndented = true });
                Log.Information("Serialized AskItems JSON: {AskItemsJson}", askItemsJson);
            }
        }
        
        var soldToIds = !string.IsNullOrEmpty(aiSpeechAssistant.Name) ? aiSpeechAssistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();
        
        var customerItemsString = await BuildCustomerItemsStringAsync(soldToIds, cancellationToken);

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

        var extractedOrders = await ExtractAndMatchOrderItemsFromReportAsync(record.TranscriptionText, historyItems, DateTime.Today, cancellationToken).ConfigureAwait(false); 
        if (!extractedOrders.Any()) return;
        
        var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var pacificNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pacificZone);
        
        foreach (var storeOrder in extractedOrders)
        { 
            var soldToId = await ResolveSoldToIdAsync(storeOrder, aiSpeechAssistant, soldToIds, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(soldToId)) 
            { 
                Log.Warning("未能获取店铺 SoldToId, StoreName={StoreName}, StoreNumber={StoreNumber}", storeOrder.StoreName, storeOrder.StoreNumber); 
            }
             
            foreach (var item in storeOrder.Orders)
            { 
                item.MaterialNumber = MatchMaterialNumber(item.Name, item.MaterialNumber, item.Unit, historyItems); 
            }
             
            var draftOrder = CreateDraftOrder(storeOrder, soldToId, aiSpeechAssistant, pacificZone, pacificNow);
            Log.Information("DraftOrder for Store {StoreName}/{StoreNumber}: {@DraftOrder}", storeOrder.StoreName, storeOrder.StoreNumber, draftOrder);
            
            var response = await _salesClient.GenerateAiOrdersAsync(draftOrder, cancellationToken).ConfigureAwait(false); 
            Log.Information("Generate Ai Order response for Store {StoreName}/{StoreNumber}: {@response}", storeOrder.StoreName, storeOrder.StoreNumber, response);
            
            if (response?.Data != null && response.Data.OrderId != Guid.Empty) 
            { 
                await UpdateRecordOrderIdAsync(record, response.Data.OrderId, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<List<ExtractedOrderDto>> ExtractAndMatchOrderItemsFromReportAsync(string reportText, List<(string Material, string MaterialDesc)> historyItems, DateTime orderDate, CancellationToken cancellationToken) 
    { 
        var client = new ChatClient("gpt-4.1", _openAiSettings.ApiKey);
        
        var materialListText = string.Join("\n", historyItems.Select(x => $"{x.MaterialDesc} ({x.Material})"));
        
        var systemPrompt =
            "你是一名訂單分析助手。請從下面的客戶分析報告文字中提取所有下單的物料名稱、數量、單位，並且用歷史物料列表盡力匹配每個物料的materialNumber。" +
            "如果報告中提到了預約送貨時間，請提取送貨時間（格式yyyy-MM-dd）。" +
            "如果客戶提到了分店名，請提取 StoreName；如果提到第幾家店，請提取 StoreNumber。\n" +
            "請嚴格傳回一個 JSON 對象，頂層字段為 \"stores\"，每个店铺对象包含：StoreName（可空字符串）, StoreNumber（可空字符串）, DeliveryDate（可空字符串），orders（数组，元素包含 name, quantity, unit, materialNumber, deliveryDate）。\n" +
            "範例：\n" +
            "{\n    \"stores\": [\n        {\n            \"StoreName\": \"HaiDiLao\",\n            \"StoreNumber\": \"1\",\n            \"DeliveryDate\": \"2025-08-20\",\n            \"orders\": [\n                {\n                    \"name\": \"雞胸肉\",\n                    \"quantity\": 1,\n                    \"unit\": \"箱\",\n                    \"materialNumber\": \"000000000010010253\"\n                }\n            ]\n        }\n    ]\n}" +
            "歷史物料列表：\n" + materialListText + "\n\n" +
            "注意：\n1. 必須嚴格輸出 JSON，物件頂層字段必須是 \"stores\"，不要有其他字段或額外說明。\n2. 提取的物料名稱需要為繁體中文。\n3. 如果没有提到店铺信息，但是有下单内容，则StoreName和StoreNumber可为空值，orders要正常提取。\n4. **如果客戶分析文本中沒有任何可識別的下單信息，請返回：{ \"stores\": [] }。不得臆造或猜測物料。**";
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
                    DeliveryDate = storeElement.TryGetProperty("DeliveryDate", out var dd) && DateTime.TryParse(dd.GetString(), out var dt) ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(1)
                }; 
                
                if (storeElement.TryGetProperty("orders", out var ordersArray)) 
                { 
                    foreach (var orderItem in ordersArray.EnumerateArray()) 
                    { 
                        var name = orderItem.TryGetProperty("name", out var n) ? n.GetString() ?? "" : ""; 
                        var qty = orderItem.TryGetProperty("quantity", out var q) && q.TryGetDecimal(out var dec) ? dec : 0; 
                        var unit = orderItem.TryGetProperty("unit", out var u) ? u.GetString() ?? "" : ""; 
                        var materialNumber = orderItem.TryGetProperty("materialNumber", out var mn) ? mn.GetString() ?? "" : ""; 
                        
                        materialNumber = MatchMaterialNumber(name, materialNumber, unit, historyItems);

                        storeDto.Orders.Add(new ExtractedOrderItemDto
                        {
                            Name = name,
                            Quantity = (int)qty,
                            MaterialNumber = materialNumber,
                            Unit = unit
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
    
    private async Task<List<(string Material, string MaterialDesc)>> GetCustomerHistoryItemsBySoldToIdAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        List<(string Material, string MaterialDesc)> historyItems = new List<(string, string)>();

        var askInfoResponse = await _salesClient.GetAskInfoDetailListByCustomerAsync(new GetAskInfoDetailListByCustomerRequestDto { CustomerNumbers = soldToIds }, cancellationToken).ConfigureAwait(false);
        var orderHistoryResponse = await _salesClient.GetOrderHistoryByCustomerAsync(new GetOrderHistoryByCustomerRequestDto { CustomerNumber = soldToIds.FirstOrDefault() }, cancellationToken).ConfigureAwait(false);
        
        if (askInfoResponse?.Data != null && askInfoResponse.Data.Any())
            historyItems.AddRange(askInfoResponse.Data.Where(x => !string.IsNullOrWhiteSpace(x.Material)).Select(x => (x.Material, x.MaterialDesc)));
        
        if (orderHistoryResponse?.Data != null && orderHistoryResponse.Data.Any())
            historyItems.AddRange(orderHistoryResponse?.Data.Where(x => !string.IsNullOrWhiteSpace(x.MaterialNumber)).Select(x => (x.MaterialNumber, x.MaterialDescription)) ?? new List<(string, string)>());

        return historyItems;
    }
    
    private TranscriptionLanguage SelectReportLanguageEnum(string language)
    {
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return TranscriptionLanguage.Chinese;
    
        return TranscriptionLanguage.English;
    }
    
    private string MatchMaterialNumber(string itemName, string baseNumber, string unit, List<(string Material, string MaterialDesc)> historyItems)
    {
        var candidates = historyItems.Where(x => x.MaterialDesc != null && x.MaterialDesc.Contains(itemName, StringComparison.OrdinalIgnoreCase)).Select(x => x.Material).ToList();
        Log.Information("Candidate material code list: {@Candidates}", candidates);
        
        if (!candidates.Any()) return string.IsNullOrEmpty(baseNumber) ? "" : baseNumber;; 
        if (candidates.Count == 1) return candidates.First();
        
        if (!string.IsNullOrWhiteSpace(unit))
        {
            var u = unit.ToLower();
            if (u.Contains("case") || u.Contains("箱"))
            {
                var csItem = candidates.FirstOrDefault(x => x.EndsWith("CS", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(csItem)) return csItem;
            }
            else
            {
                var pcItem = candidates.FirstOrDefault(x => x.EndsWith("PC", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(pcItem)) return pcItem;
            }
        }
        
        var pureNumber = candidates.FirstOrDefault(x => Regex.IsMatch(x, @"^\d+$"));
        return pureNumber ?? candidates.First();
    }
    
    private async Task<string> ResolveSoldToIdAsync(ExtractedOrderDto storeOrder, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, List<string> soldToIds, CancellationToken cancellationToken) 
    { 
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
    
    private GenerateAiOrdersRequestDto CreateDraftOrder(ExtractedOrderDto storeOrder, string soldToId, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, TimeZoneInfo pacificZone, DateTime pacificNow) 
    { 
        var pacificDeliveryDate = storeOrder.DeliveryDate != default ? TimeZoneInfo.ConvertTimeFromUtc(storeOrder.DeliveryDate, pacificZone) : pacificNow.AddDays(1);

        var assistantNameWithComma = aiSpeechAssistant.Name?.Replace('/', ',') ?? string.Empty;

        return new GenerateAiOrdersRequestDto
        {
            AiModel = "Smartalk",
            AiOrderInfoDto = new AiOrderInfoDto
            {
                SoldToId = soldToId,
                SoldToIds = string.IsNullOrEmpty(soldToId) ? assistantNameWithComma : soldToId,
                DocumentDate = pacificNow.Date,
                DeliveryDate = pacificDeliveryDate.Date,
                AiOrderItemDtoList = storeOrder.Orders.Select(i => new AiOrderItemDto
                {
                    MaterialNumber = i.MaterialNumber,
                    AiMaterialDesc = i.Name,
                    MaterialQuantity = i.Quantity,
                    AiUnit = i.Unit
                }).ToList()
            }
        };
    }
    
    private async Task UpdateRecordOrderIdAsync(PhoneOrderRecord record, Guid orderId, CancellationToken cancellationToken) 
    { 
        var orderIds = string.IsNullOrEmpty(record.OrderId) ? new List<Guid>() : JsonSerializer.Deserialize<List<Guid>>(record.OrderId)!;
        
        orderIds.Add(orderId); 
        record.OrderId = JsonSerializer.Serialize(orderIds);
        
        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false); 
    }
    
    private async Task<bool> CheckCustomerFriendlyAsync(string transcriptionText, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new()
                {
                    Role = "system",
                    Content = new CompletionsStringContent("你需要帮我从电话录音报告中提取出客人态度是否友好，态度友好返回true，态度恶劣，有负面情绪返回false" +
                                       "注意用json格式返回；" +
                                       "规则：{\"IsCustomerFriendly\": true}" +
                                       "- 样本与输出：\n" + 
                                       "input:" +
                                       "通話主題：客戶下單雞脾肉\n內容摘要：客戶表示想要下一張訂單，並訂購了一箱雞脾肉，隨後表示沒有其他需要，結束通話。\n\n客戶情緒與語氣：語氣平和，態度明確。" +
                                       "output:true\n")
                },
                new()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"input: {transcriptionText}, output:")
                }
            },
            Model = OpenAiModel.Gpt4o
        }, cancellationToken).ConfigureAwait(false);

        return bool.Parse(completionResult.Data.Response);
    }
}