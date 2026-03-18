using Google.Cloud.Translation.V2;
using OpenAI.Chat;
using Serilog;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Security;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Services.STT;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.SpeechMatics;
using SmartTalk.Messages.Enums.STT;
using JsonDocument = System.Text.Json.JsonDocument;

namespace SmartTalk.Core.Services.AiKids;

public interface IAiKidRealtimeProcessJobService : IScopedDependency
{
    Task RecordingRealtimeAiAsync(string recordingUrl, int assistantId, string sessionId, PhoneOrderRecordType orderRecordType, CancellationToken cancellationToken);
}

public class AiKidRealtimeProcessJobService : IAiKidRealtimeProcessJobService
{
    private readonly TranslationClient _translationClient;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly ISpeechMaticsService _speechMaticsService;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly ISalesClient _salesClient;
    private readonly OpenAiSettings _openAiSettings;

    public AiKidRealtimeProcessJobService(
        TranslationClient translationClient,
        IAgentDataProvider agentDataProvider,
        IPhoneOrderService phoneOrderService,
        ISpeechToTextService speechToTextService,
        ISpeechMaticsService speechMaticsService,
        ISmartTalkHttpClientFactory httpClientFactory,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        ISalesClient salesClient,
        OpenAiSettings openAiSettings)
    {
        _phoneOrderService = phoneOrderService;
        _agentDataProvider = agentDataProvider;
        _translationClient = translationClient;
        _httpClientFactory = httpClientFactory;
        _speechToTextService = speechToTextService;
        _speechMaticsService = speechMaticsService;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _salesClient = salesClient;
        _openAiSettings = openAiSettings;
    }

    public async Task RecordingRealtimeAiAsync(string recordingUrl, int assistantId, string sessionId,  PhoneOrderRecordType orderRecordType, CancellationToken cancellationToken)
   {
        Log.Information("RecordingRealtimeAiAsync recording url: {recordingUrl}", recordingUrl);
        
        var agent = await _agentDataProvider.GetAgentByAssistantIdAsync(assistantId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get the agent by assistantId: {@Agent}", agent);

        if (agent == null) return;
        
        if (agent.IsSendAudioRecordWechat)
            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, agent.WechatRobotKey, $"您有一条新的AI通话录音：\n{recordingUrl}", [], CancellationToken.None).ConfigureAwait(false);
        
        var recordingContent = await _httpClientFactory.GetAsync<byte[]>(recordingUrl, cancellationToken).ConfigureAwait(false);
        if (recordingContent == null) return;
        
        var transcription = await _speechToTextService.SpeechToTextAsync(
            recordingContent, fileType: TranscriptionFileType.Wav, responseFormat: TranscriptionResponseFormat.Text, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var detection = await _translationClient.DetectLanguageAsync(transcription, cancellationToken).ConfigureAwait(false);
        
        var record = new PhoneOrderRecord { SessionId = sessionId, AgentId = agent?.Id ?? 0, TranscriptionText = transcription, Url = recordingUrl, Language = SelectLanguageEnum(detection.Language), CreatedDate = DateTimeOffset.Now, Status = PhoneOrderRecordStatus.Recieved, OrderRecordType = orderRecordType };

        await _phoneOrderDataProvider.AddPhoneOrderRecordsAsync([record], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        record.TranscriptionJobId = await _speechMaticsService.CreateSpeechMaticsJobAsync(recordingContent, Guid.NewGuid().ToString("N") + ".wav", detection.Language, SpeechMaticsJobScenario.Released, cancellationToken).ConfigureAwait(false);
        record.Status = PhoneOrderRecordStatus.Diarization;
        
        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);

        if (orderRecordType == PhoneOrderRecordType.OmeClawTest)
        {
            var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(assistantId, cancellationToken).ConfigureAwait(false);
            await TryCallBackOrderItemsForOmeClawAsync(assistant, transcription, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TryCallBackOrderItemsForOmeClawAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistant, string transcription, CancellationToken cancellationToken)
    {
        if (assistant == null || string.IsNullOrWhiteSpace(transcription)) return;

        try
        {
            var soldToIds = ParseSoldToIds(assistant.Name);
            
            var extractedOrders = await ExtractAndMatchOrderItemsFromReportAsync(transcription, cancellationToken).ConfigureAwait(false);
            
            var callbackRequest = BuildOrderItemsCallBackRequest(extractedOrders, soldToIds);

            var response = await _httpClientFactory
                .PostAsJsonAsync("http://tomsite.com/ai/order/items/callback", callbackRequest, cancellationToken)
                .ConfigureAwait(false);

            Log.Information(
                "Ai kid order items callback finished. AssistantId={AssistantId}, CustomerId={CustomerId}, ItemCount={ItemCount}, StatusCode={StatusCode}",
                assistant.Id,
                callbackRequest.CustomerId,
                callbackRequest.Items.Count,
                response?.StatusCode);
            
            Log.Information(
                "Ai kid order items callback finished. call back json={AssistantId},",Newtonsoft.Json.JsonConvert.SerializeObject(callbackRequest));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ai kid order items callback failed. AssistantId={AssistantId}", assistant.Id);
        }
    }

    private static List<string> ParseSoldToIds(string assistantName)
    {
        if (string.IsNullOrWhiteSpace(assistantName)) return [];

        return assistantName
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private async Task<List<(string Material, string MaterialDesc, DateTime? InvoiceDate)>> GetCustomerHistoryItemsBySoldToIdAsync(
        List<string> soldToIds, CancellationToken cancellationToken)
    {
        if (soldToIds == null || soldToIds.Count == 0) return [];

        List<(string Material, string MaterialDesc, DateTime? InvoiceDate)> historyItems = [];

        var askInfoResponse = await _salesClient
            .GetAskInfoDetailListByCustomerAsync(
                new GetAskInfoDetailListByCustomerRequestDto { CustomerNumbers = soldToIds }, cancellationToken)
            .ConfigureAwait(false);
        var orderHistoryResponse = await _salesClient
            .GetOrderHistoryByCustomerAsync(
                new GetOrderHistoryByCustomerRequestDto { CustomerNumber = soldToIds.FirstOrDefault() },
                cancellationToken).ConfigureAwait(false);

        if (askInfoResponse?.Data != null && askInfoResponse.Data.Any())
            historyItems.AddRange(askInfoResponse.Data.Where(x => !string.IsNullOrWhiteSpace(x.Material))
                .Select(x => (x.Material, x.MaterialDesc, (DateTime?)null)));

        if (orderHistoryResponse?.Data != null && orderHistoryResponse.Data.Any())
            historyItems.AddRange(
                orderHistoryResponse.Data.Where(x => !string.IsNullOrWhiteSpace(x.MaterialNumber))
                    .Select(x => (x.MaterialNumber, x.MaterialDescription, x.LastInvoiceDate)));

        return historyItems;
    }

    private async Task<List<ExtractedOrderDto>> ExtractAndMatchOrderItemsFromReportAsync(string reportText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportText)) return [];

        var client = new ChatClient("gpt-4.1", _openAiSettings.ApiKey);
        
        var systemPrompt =
            "你是一名订单分析助手。请从下面的通话文字中，提取客户下单信息，并严格返回 JSON 结果。\n\n" +
            "提取规则如下：\n" +
            "1. 提取所有下单的物料，包含以下字段：name、quantity、unit。\n" +
            "2. 如果通话内容中提到了预约送货时间，请提取 DeliveryDate，格式必须为 yyyy-MM-dd。\n" +
            "3. 如果客户提到了分店名称，请提取 StoreName；如果提到了第几家店，请提取 StoreNumber。\n" +
            "4. 如果没有提到店铺信息，但存在下单内容，则 StoreName 和 StoreNumber 允许为空字符串。\n" +
            "5. 提取的物料名称必须使用繁体中文。\n" +
            "6. 必须尽可能完整提取文本中每一个明确提到的下单物料，不得遗漏。\n" +
            "7. 不得臆造、补全或猜测文本中未明确提到的信息。\n\n" +
            "输出要求如下：\n" +
            "1. 仅可输出一个 JSON 对象，不得包含任何额外说明、注释或其他文字。\n" +
            "2. JSON 顶层字段必须为 \"stores\"。\n" +
            "3. 每个店铺对象包含以下字段：StoreName、StoreNumber、DeliveryDate、orders。\n" +
            "4. orders 为数组，数组元素包含：name、quantity、unit。\n" +
            "5. 如果文本中没有任何可识别的下单信息，请返回：{\"stores\":[]}。\n\n" +
            "输出格式示例：\n" +
            "{\n" +
            "  \"stores\": [\n" +
            "    {\n" +
            "      \"StoreName\": \"海底撈\",\n" +
            "      \"StoreNumber\": \"1\",\n" +
            "      \"DeliveryDate\": \"2025-08-20\",\n" +
            "      \"orders\": [\n" +
            "        {\n" +
            "          \"name\": \"雞胸肉\",\n" +
            "          \"quantity\": 1,\n" +
            "          \"unit\": \"箱\"\n" +
            "        }\n" +
            "      ]\n" +
            "    }\n" +
            "  ]\n" +
            "}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage("通话文本：\n" + reportText + "\n\n")
        };

        var completion = await client.CompleteChatAsync(messages,
            new ChatCompletionOptions
            {
                ResponseModalities = ChatResponseModalities.Text,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            }, cancellationToken).ConfigureAwait(false);
        var jsonResponse = completion.Value.Content.FirstOrDefault()?.Text ?? "";

        try
        {
            using var jsonDoc = JsonDocument.Parse(jsonResponse);

            if (!jsonDoc.RootElement.TryGetProperty("stores", out var storesArray)) return [];

            var results = new List<ExtractedOrderDto>();

            foreach (var storeElement in storesArray.EnumerateArray())
            {
                var storeDto = new ExtractedOrderDto
                {
                    StoreName = storeElement.TryGetProperty("StoreName", out var sn) ? sn.GetString() ?? "" : "",
                    StoreNumber = storeElement.TryGetProperty("StoreNumber", out var snum) ? snum.GetString() ?? "" : "",
                    DeliveryDate =
                        storeElement.TryGetProperty("DeliveryDate", out var dd) &&
                        DateTime.TryParse(dd.GetString(), out var dt)
                            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                            : DateTime.UtcNow
                };

                if (storeElement.TryGetProperty("orders", out var ordersArray))
                {
                    foreach (var orderItem in ordersArray.EnumerateArray())
                    {
                        var name = orderItem.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var qty = orderItem.TryGetProperty("quantity", out var q) && q.TryGetDecimal(out var dec) ? dec : 0;
                        var unit = orderItem.TryGetProperty("unit", out var u) ? u.GetString() ?? "" : "";
                 
                        storeDto.Orders.Add(new ExtractedOrderItemDto
                        {
                            AiMaterialDesc = name,
                            Quantity = (int)qty,
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
            Log.Warning("Ai kid parsing order items JSON failed: {Message}, Json: {Json}", ex.Message, jsonResponse);
            return [];
        }
    }


    private static AiKidOrderItemsCallBackRequestDto BuildOrderItemsCallBackRequest(
        List<ExtractedOrderDto> extractedOrders, List<string> soldToIds)
    {
        var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var pacificNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pacificZone);
        var deliveryDate = extractedOrders
            .Select(x => x.DeliveryDate)
            .Where(x => x != default)
            .OrderBy(x => x)
            .FirstOrDefault();

        return new AiKidOrderItemsCallBackRequestDto
        {
            DocDate = pacificNow.ToString("yyyy-MM-dd"),
            DeliveryDate = (deliveryDate == default ? pacificNow : deliveryDate).ToString("yyyy-MM-dd"),
            CustomerId = soldToIds.FirstOrDefault() ?? "",
            Items = extractedOrders
                .SelectMany(x => x.Orders)
                .Where(x => !string.IsNullOrWhiteSpace(x.AiMaterialDesc))
                .GroupBy(x => new { x.AiMaterialDesc, x.Unit })
                .Select(x => new AiKidOrderItemDto
                {
                    Name = x.Key.AiMaterialDesc,
                    Unit = x.Key.Unit ?? "",
                    Qty = Math.Max(1, x.Sum(y => y.Quantity))
                })
                .ToList()
        };
    }

    private TranscriptionLanguage SelectLanguageEnum(string language)
    {
        return language switch
        {
            "zh" or "zh-CN" or "zh-TW" => TranscriptionLanguage.Chinese,
            _ => TranscriptionLanguage.English
        };
    }
}
