using AutoMapper;
using Newtonsoft.Json;
using OpenAI.Chat;
using Serilog;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Core.Services.Pos;

public interface IPosUtilService : IScopedDependency
{
    Task GenerateAiDraftAsync(Agent agent, Domain.AISpeechAssistant.AiSpeechAssistant assistant, PhoneOrderRecord record, CancellationToken cancellationToken);

    Task<(List<PosProduct> Products, string MenuItems)> GeneratePosMenuItemsAsync(int agentId, bool isWithProductId = false, TranscriptionLanguage language = TranscriptionLanguage.Chinese, CancellationToken cancellationToken = default);
    
    Task<(string simpleItems, decimal? amount)> CalculateOrderAmountAsync(AiSpeechAssistantDto assistant, BinaryData audioData, CancellationToken cancellationToken = default);
}

public class PosUtilService : IPosUtilService
{
    private readonly IMapper _mapper;
    private readonly IPosService _posService;
    private readonly OpenAiSettings _openAiSettings;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IRedisSafeRunner _redisSafeRunner;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;

    public PosUtilService(IMapper mapper, IPosService posService, OpenAiSettings openAiSettings, IPosDataProvider posDataProvider, IRedisSafeRunner redisSafeRunner, IAgentDataProvider agentDataProvider, IPhoneOrderDataProvider phoneOrderDataProvider)
    {
        _mapper = mapper;
        _posService = posService;
        _openAiSettings = openAiSettings;
        _posDataProvider = posDataProvider;
        _redisSafeRunner = redisSafeRunner;
        _agentDataProvider = agentDataProvider;
        _phoneOrderDataProvider = phoneOrderDataProvider;
    }
    
    public async Task GenerateAiDraftAsync(Agent agent, Domain.AISpeechAssistant.AiSpeechAssistant assistant, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        if (record is not { Scenario: DialogueScenarios.Order })
        {
            Log.Information("The scenario is not the order scenario: {@Record}.", record);
            
            return;
        }
        
        if (agent.Type != AgentType.PosCompanyStore || !assistant.IsAutoGenerateOrder) return;
        
        var posOrder = await _posDataProvider.GetPosOrderByIdAsync(recordId: record.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (posOrder != null)
        {
            Log.Information("The order already exist: {@PosOrder}, recordId: {RecordId}", posOrder, record.Id);
            
            return;
        }
        
        try
        {
            var (matchedProducts, aiDraftOrder) = await ExtractProductsFromReportAsync(agent, record.Id, record.TranscriptionText, record.IncomingCallNumber, record.Language, cancellationToken).ConfigureAwait(false);
            
            if (matchedProducts == null || aiDraftOrder == null) return;

            var order = await BuildPosOrderAsync(record, aiDraftOrder, matchedProducts, cancellationToken).ConfigureAwait(false);

            if (assistant.IsAllowOrderPush)
                await _posService.HandlePosOrderAsync(order, false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error(e, "Generate ai draft order failed.");
        }
    }

    private async Task<(List<PosProduct> matchedProducts, AiDraftOrderDto aiDraftOrder)> ExtractProductsFromReportAsync(Agent agent, int recordId, string report, string incomingCallNumber, TranscriptionLanguage language,  CancellationToken cancellationToken)
    {
        var originalReport = await _phoneOrderDataProvider.GetOriginalPhoneOrderRecordReportAsync(recordId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(originalReport?.Report)) report = originalReport.Report;
        
        var (products, menuItems) = await GeneratePosMenuItemsAsync(agent.Id, true, language, cancellationToken).ConfigureAwait(false);

        var client = new ChatClient("gpt-4.1", _openAiSettings.ApiKey);

        var systemPrompt =
            "你是一名訂單分析助手。請從下面的客戶分析報告文字中提取客人的姓名、电话、配送类型以及配送地址，以及所有下單的菜品、數量、規格、备注，並且用菜單列表盡力匹配每個菜品。\n" +
            "如果報告中提到了送餐類型，請提取送餐類型 type (0: 自提订单，1：配送订单)。\n" +
            "如果客户有要求或者提供其他的号码作为订单的号码，請提取客户的电话 phoneNumber ，否则 phoneNumber 为当前的来电号码：" + incomingCallNumber + "。\n"+
            "如果報告中提到了客户的姓名，請提取客户的姓名 customerName 。\n" +
            "如果報告中提到了客户的配送地址，請提取客户的配送地址 customerAddress，若无则忽略 。\n" +
            "如果報告中提到了客户的订单注意事项或者是要求，且該內容不能獨立構成一個可下單的菜品名稱，則請提取為备注信息 notes；若该要求是附属于某一道菜品的特殊交代（如口味、加料、忌口），則在不影響該菜品正常生成 items 的前提下，將該要求體現在 notes 中。\n" +
            "另外请注意备注的语言，当前的语言为: " + language.GetDescription() + "，如果当前语言类型为 zh，则备注为中文，若不是 zh，则备注为英文 \n" +
            "請嚴格傳回一個 JSON 對象，頂層字段為 \"type\"，items（数组，元素包含 productId：菜品ID, name：菜品名, quantity：数量, specification：规格（比如：大、中、小，加小料、加椰果或者有关菜品的其他内容））。\n" +
            "範例：\n" +
            "{\"type\":0,\"phoneNumber\":\"40085235698\",\"customerName\":\"刘先生\",\"customerAddress\":\"中环广场一座\",\"notes\":\"给个酱油包\",\"items\":[{\"productId\":\"9778779965031491\",\"name\":\"海南雞湯麵\",\"quantity\":1,\"specification\":null}]}" +
            "{\"type\":1,\"phoneNumber\":\"40026235458\",\"customerName\":\"吴先生\",\"customerAddress\":\"中环广场三座\",\"notes\":\"到了不要敲门，放门口\",\"items\":[{\"productId\":\"9225097809167409\",\"name\":\"港式燒鴨\",\"quantity\":1,\"specification\":\"半隻\"}]} \n\n" +
            "菜單列表：\n" + menuItems + "\n\n" +
            "注意：\n1. 必須嚴格按格式輸出 JSON，不要有其他字段或額外說明。\n2. **如果客戶分析文本中沒有任何可識別的下單信息，請返回：{ \"type\":0, \"items\": [] }。不得臆造或猜測菜品。** \n" +
            "請務必完整提取報告中每一個提到的菜品";

        Log.Information("Sending prompt with menu items to GPT: {Prompt}", systemPrompt);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage("客戶分析報告文本：\n" + report + "\n\n")
        };

        var completion = await client.CompleteChatAsync(messages, new ChatCompletionOptions { ResponseModalities = ChatResponseModalities.Text, ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() }, cancellationToken).ConfigureAwait(false);

        try
        {
            var aiDraftOrder = JsonConvert.DeserializeObject<AiDraftOrderDto>(completion.Value.Content.FirstOrDefault()?.Text ?? "");

            Log.Information("Deserialize response to ai order: {@AiOrder}", aiDraftOrder);

            var matchedProducts = products.Where(x => aiDraftOrder.Items.Select(p => p.ProductId).Contains(x.ProductId)).DistinctBy(x => x.ProductId).ToList();

            Log.Information("Matched products: {@MatchedProducts}", matchedProducts);

            var productModifiersLookup = matchedProducts.Where(x => !string.IsNullOrWhiteSpace(x.Modifiers))
                .ToDictionary(x => x.ProductId, x => JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(x.Modifiers));

            Log.Information("Build product's modifiers: {@ModifiersLookUp}", productModifiersLookup);

            foreach (var aiDraftItem in aiDraftOrder.Items.Where(x => !string.IsNullOrEmpty(x.Specification) && !string.IsNullOrEmpty(x.ProductId)))
            {
                if (productModifiersLookup.TryGetValue(aiDraftItem.ProductId, out var modifiers))
                {
                    try
                    {
                        var builtModifiers = await GenerateSpecificationProductsAsync(modifiers, language, aiDraftItem.Specification, cancellationToken).ConfigureAwait(false);
                        
                        Log.Information("Matched modifiers: {@MatchedModifiers}", builtModifiers);

                        if (builtModifiers == null || builtModifiers.Count == 0) continue;

                        aiDraftItem.Modifiers = builtModifiers;
                    }
                    catch (Exception e)
                    {
                        aiDraftItem.Modifiers = [];
                        
                        Log.Error(e, "Failed to build product: {@AiDraftItem} modifiers", aiDraftItem);
                    }
                }
            }

            Log.Information("Enrich ai draft order: {@EnrichAiDraftOrder}", aiDraftOrder);

            return (matchedProducts, aiDraftOrder);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to extract products from report text");

            return (null, null);
        }
    }

    public async Task<List<AiDraftItemModifiersDto>> GenerateSpecificationProductsAsync(List<EasyPosResponseModifierGroups> modifiers, TranscriptionLanguage language, string specification, CancellationToken cancellationToken)
    {
        var client = new ChatClient("gpt-4.1", _openAiSettings.ApiKey);

        var builtModifiers = BuildItemModifiers(modifiers);

        if (string.IsNullOrWhiteSpace(builtModifiers)) return [];

        var systemPrompt =
            "你是一名菜品规格提取助手。請從下面的规格菜品中提取所有的规格菜品的ID、數量，並且用规格菜單列表盡力匹配每個规格菜品。" +
            "請嚴格傳回一個 JSON 对象，頂層字段為 modifiers（数组，元素包含 id：规格ID,  quantity：数量）。\n" +
            "範例：\n" +
            "若最少可选规格数量为1，最多可选规格数量为3，规格每个的最大可选数量为2，则输出为：{\"modifiers\":[{\"id\": \"11545690032571397\", \"quantity\": 1}]}" +
            "若最少可选规格数量为1，最多可选规格数量为3，规格每个的最大可选数量为2，则输出为：{\"modifiers\":[{\"id\": \"11545690032571397\", \"quantity\": 1},{\"id\": \"11545690055571397\", \"quantity\": 2},{\"id\": \"11545958055571397\", \"quantity\": 2}]}" +
            "規格列表：\n" + builtModifiers + "\n\n" +
            "注意：\n1. 必須嚴格按格式輸出 JSON，不要有其他字段或額外說明。\n" +
            "請務必完整提取報告中每一個提到的菜品";

        Log.Information("Sending prompt with modifier items to GPT: {Prompt}", systemPrompt);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage("规格菜品：\n" + specification + "\n\n")
        };

        var completion = await client.CompleteChatAsync(messages, new ChatCompletionOptions { ResponseModalities = ChatResponseModalities.Text, ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() }, cancellationToken).ConfigureAwait(false);

        var result = JsonConvert.DeserializeObject<AiDraftItemSpecificationDto>(completion.Value.Content.FirstOrDefault()?.Text ?? "");
        
        Log.Information("Deserialize response to ai specification: {@Result}", result);
        
        return result.Modifiers;
    }
    
     public async Task<(List<PosProduct> Products, string MenuItems)> GeneratePosMenuItemsAsync(int agentId, bool isWithProductId = false, TranscriptionLanguage language = TranscriptionLanguage.Chinese, CancellationToken cancellationToken = default)
    {
        var storeAgent = (await _posDataProvider.GetPosAgentByAgentIdsAsync([agentId], cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (storeAgent == null) return ([], null);

        var categoryProductsPairs = await _posDataProvider.GetPosCategoryAndProductsAsync(storeAgent.StoreId, cancellationToken).ConfigureAwait(false);

        var categoryProductsLookup = categoryProductsPairs.GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.Select(p => p.Item2).DistinctBy(p => p.ProductId).ToList());

        var menuItems = string.Empty;

        foreach (var (category, products) in categoryProductsLookup)
        {
            var productDetails = string.Empty; 
            var categoryNames = JsonConvert.DeserializeObject<PosNamesLocalization>(category.Names);

            var idx = 1;
            var categoryName = BuildMenuItemName(categoryNames, language);

            if (string.IsNullOrWhiteSpace(categoryName)) continue;
            
            productDetails += categoryName + "\n";

            foreach (var product in products)
            {
                var productNames = JsonConvert.DeserializeObject<PosNamesLocalization>(product.Names);
                
                var productName = BuildMenuItemName(productNames, language);

                if (string.IsNullOrWhiteSpace(productName)) continue;
                
                var line = $"{idx}. {productName}{(isWithProductId ? $"({product.ProductId})" : "")}：${product.Price:F2}";

                idx++;
                productDetails += line + "\n";
            }

            menuItems += productDetails + "\n";
        }

        return (categoryProductsLookup.SelectMany(x => x.Value).ToList(), menuItems.TrimEnd('\r', '\n'));
    }

    public async Task<(string simpleItems, decimal? amount)> CalculateOrderAmountAsync(
       AiSpeechAssistantDto assistant, BinaryData audioData, CancellationToken cancellationToken = default)
    {
        var agent = await _agentDataProvider.GetAgentByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get agent: {@Agent} by assistant id: {AssistantId}", agent, assistant.Id);
        
        if (agent == null) return (string.Empty, null);

        var report = await GenerateAiDraftReportAsync(agent, audioData, cancellationToken).ConfigureAwait(false);

        var (matchedProducts, aiDraftOrder) = await ExtractProductsFromReportAsync(agent, 0, report, string.Empty, TranscriptionLanguage.Chinese, cancellationToken).ConfigureAwait(false);
            
        var draftMapping = BuildAiDraftAndProductMapping(matchedProducts, aiDraftOrder.Items);
        
        var (items, subTotal, taxes) = BuildPosOrderItems(draftMapping);

        var simpleItems = string.Join("、", items.Select(x => x.ProductName + x.Price));
        
        return (simpleItems, subTotal + taxes);
    }

    private async Task<string> GenerateAiDraftReportAsync(Agent agent, BinaryData audioData, CancellationToken cancellationToken)
    {
        var (_, menuItems) = await GeneratePosMenuItemsAsync(agent.Id, false, TranscriptionLanguage.Chinese, cancellationToken).ConfigureAwait(false);
        
        List<ChatMessage> messages =
        [
            new SystemChatMessage("你是一名電話訂單內容分析員，只需要從錄音內容中提取客戶實際下單的菜品資訊。\n\n" +
                                  "請僅輸出「客戶下單內容」，不要輸出任何其他說明、分析、摘要或多餘文字。\n\n" +
                                  "輸出格式必須嚴格遵守以下結構（若無有效下單內容，則不輸出任何內容）：\n\n" +
                                  "客戶下單內容（請務必按該格式輸出：菜品 數量 規格）：\n" +
                                  "1. 港式奶茶 2杯\n" +
                                  "2. 海南雞湯麵 2份\n" +
                                  "3. 叉燒包派對餐 1份 小份(12pcs)\n\n" +
                                  "注意事項：\n- 僅提取客戶明確確認的下單菜品\n" +
                                  "- 不要推測或補全未明確提及的菜品\n" +
                                  "- 規格（如大小、數量、套餐）僅在錄音中明確出現時才輸出\n" +
                                  "- 若客戶未下單或未識別到有效菜品，請不要輸出任何內容\n\n" +
                                  "客戶下單的菜品可能參考以下菜單，但不限於此：\n" + menuItems),
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage("幫我根據錄音生成订单草稿：")
        ];
        
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);
 
        ChatCompletionOptions options = new() { ResponseModalities = ChatResponseModalities.Text };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        
        Log.Information("Calculate: summary report:" + completion.Content.FirstOrDefault()?.Text);
        
        return completion.Content.FirstOrDefault()?.Text ?? string.Empty;
    }

    private string BuildItemModifiers(List<EasyPosResponseModifierGroups> modifiers, TranscriptionLanguage language = TranscriptionLanguage.Chinese)
    {
        if (modifiers == null || modifiers.Count == 0) return null;

        var modifiersDetail = string.Empty;

        foreach (var modifier in modifiers)
        {
            var modifierNames = new List<string>();

            if (modifier.ModifierProducts != null && modifier.ModifierProducts.Count != 0)
            {
                foreach (var mp in modifier.ModifierProducts)
                {
                    var name = BuildModifierName(mp.Localizations, language);

                    if (!string.IsNullOrWhiteSpace(name)) modifierNames.Add($"{name}({mp.Id})");
                }
            }

            if (modifierNames.Count > 0)
                modifiersDetail += $"{BuildModifierName(modifier.Localizations, language)}規格：{string.Join("、", modifierNames)}，共{modifierNames.Count}个规格，要求最少选{modifier.MinimumSelect}个规格，最多选{modifier.MaximumSelect}规格，每个最大可重复选{modifier.MaximumRepetition}相同的 \n";
        }

        return modifiersDetail.TrimEnd('\r', '\n');
    }
    
    private string BuildMenuItemName(PosNamesLocalization localization, TranscriptionLanguage language = TranscriptionLanguage.Chinese)
    {
        if (language is TranscriptionLanguage.Chinese)
        {
            var zhName = !string.IsNullOrWhiteSpace(localization?.Cn?.Name) ? localization.Cn.Name : string.Empty;
            if (!string.IsNullOrWhiteSpace(zhName)) return zhName;
    
            var zhPosName = !string.IsNullOrWhiteSpace(localization?.Cn?.PosName) ? localization.Cn.PosName : string.Empty;
            if (!string.IsNullOrWhiteSpace(zhPosName)) return zhPosName;
    
            var zhSendChefName = !string.IsNullOrWhiteSpace(localization?.Cn?.SendChefName) ? localization.Cn.SendChefName : string.Empty;
            if (!string.IsNullOrWhiteSpace(zhSendChefName)) return zhSendChefName;
        }
        
        var usName = !string.IsNullOrWhiteSpace(localization?.En?.Name) ? localization.En.Name : string.Empty;
        if (!string.IsNullOrWhiteSpace(usName)) return usName;
            
        var usPosName = !string.IsNullOrWhiteSpace(localization?.En?.PosName) ? localization.En.PosName : string.Empty;
        if (!string.IsNullOrWhiteSpace(usPosName)) return usPosName;
            
        var usSendChefName = !string.IsNullOrWhiteSpace(localization?.En?.SendChefName) ? localization.En.SendChefName : string.Empty;
        if (!string.IsNullOrWhiteSpace(usSendChefName)) return usSendChefName;

        return string.Empty;
    }
    
    private string BuildModifierName(List<EasyPosResponseLocalization> localizations, TranscriptionLanguage language)
    {
        if (language is TranscriptionLanguage.Chinese)
        {
            var zhName = localizations.Find(l => l.LanguageCode == "zh_CN" && l.Field == "name");
            if (zhName != null && !string.IsNullOrWhiteSpace(zhName.Value)) return zhName.Value;
            
            var zhPosName = localizations.Find(l => l.LanguageCode == "zh_CN" && l.Field == "posName");
            if (zhPosName != null && !string.IsNullOrWhiteSpace(zhPosName.Value)) return zhPosName.Value;
            
            var zhSendChefName = localizations.Find(l => l.LanguageCode == "zh_CN" && l.Field == "sendChefName");
            if (zhSendChefName != null && !string.IsNullOrWhiteSpace(zhSendChefName.Value)) return zhSendChefName.Value;
        }
        
        var usName = localizations.Find(l => l.LanguageCode == "en_US" && l.Field == "name");
        if (usName != null && !string.IsNullOrWhiteSpace(usName.Value)) return usName.Value;
        
        var usPosName = localizations.Find(l => l.LanguageCode == "en_US" && l.Field == "posName");
        if (usPosName != null && !string.IsNullOrWhiteSpace(usPosName.Value)) return usPosName.Value;
        
        var usSendChefName = localizations.Find(l => l.LanguageCode == "en_US" && l.Field == "sendChefName");
        if (usSendChefName != null && !string.IsNullOrWhiteSpace(usSendChefName.Value)) return usSendChefName.Value;

        return string.Empty;
    }

    private async Task<PosOrder> BuildPosOrderAsync(PhoneOrderRecord record, AiDraftOrderDto aiDraftOrder, List<PosProduct> products, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosStoreByAgentIdAsync(record.AgentId, cancellationToken).ConfigureAwait(false);
        
        var draftMapping = BuildAiDraftAndProductMapping(products, aiDraftOrder.Items);
        
        return await _redisSafeRunner.ExecuteWithLockAsync($"generate-order-number-{store.Id}", async() =>
        {
            var (items, subTotal, taxes) = BuildPosOrderItems(draftMapping);
            
            var orderNo = await GenerateOrderNumberAsync(store, cancellationToken).ConfigureAwait(false);

            var phoneNUmber = !string.IsNullOrWhiteSpace(aiDraftOrder?.PhoneNumber)
                ? aiDraftOrder?.PhoneNumber.Replace("+1", "").Replace("-", "") : !string.IsNullOrWhiteSpace(record?.IncomingCallNumber)
                    ? record.IncomingCallNumber.Replace("+1", "") : "Unknown";
            
            var order = new PosOrder
            {
                StoreId = store.Id,
                Name = !string.IsNullOrEmpty(aiDraftOrder?.CustomerName) ? aiDraftOrder.CustomerName : record?.CustomerName ?? "Unknown",
                Phone = phoneNUmber,
                Address = aiDraftOrder?.CustomerAddress,
                OrderNo = orderNo,
                Status = PosOrderStatus.Pending,
                Count = items.Sum(x => x.Quantity),
                Tax = taxes,
                Total = subTotal + taxes,
                SubTotal = subTotal,
                Type = (PosOrderReceiveType)aiDraftOrder.Type,
                Items = JsonConvert.SerializeObject(items),
                Notes = aiDraftOrder?.Notes ?? string.Empty,
                RecordId = record!.Id
            };
            
            Log.Information("Generate complete order: {@Order}", order);
        
            await _posDataProvider.AddPosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
            
            return order;
        }, wait: TimeSpan.FromSeconds(10), retry: TimeSpan.FromSeconds(1), server: RedisServer.System).ConfigureAwait(false);
    }

    private List<(AiDraftItemDto Item, PosProduct Product)> BuildAiDraftAndProductMapping(List<PosProduct> products, List<AiDraftItemDto> items)
    {
        var mapping = new Dictionary<AiDraftItemDto, PosProduct>();
        
        foreach (var item in items)
        {
            var product = products.Where(x => x.ProductId == item.ProductId).FirstOrDefault();

            if (product == null) continue;
            
            mapping.Add(item, product);
        }
        
        return mapping.Select(x => (x.Key, x.Value)).ToList();
    }

    private async Task<string> GenerateOrderNumberAsync(CompanyStore store, CancellationToken cancellationToken)
    {
        var (utcStart,utcEnd) = GetUtcMidnightForTimeZone(DateTimeOffset.UtcNow, store.Timezone);
        
        var preOrder = await _posDataProvider.GetPosOrderSortByOrderNoAsync(store.Id, utcStart, utcEnd, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (preOrder == null) return "0001";

        var rs = Convert.ToInt32(preOrder.OrderNo);
        
        rs++;
        
        return rs.ToString("D4");
    }
    
    private string TimezoneMapping(string timezone)
    {
        if (string.IsNullOrEmpty(timezone)) return "Pacific Standard Time";
        
        return timezone.Trim() switch
        {
            "America/Los_Angeles" => "Pacific Standard Time",
            _ => timezone.Trim()
        };
    }
    
    private (DateTimeOffset utcStart, DateTimeOffset utcEnd) GetUtcMidnightForTimeZone(DateTimeOffset utcNow, string timezone)
    {
        var windowsId = TimezoneMapping(timezone);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        
        var localTime = TimeZoneInfo.ConvertTime(utcNow, tz);
        var localMidnight = new DateTime(localTime.Year, localTime.Month, localTime.Day, 0, 0, 0);
        var localStart = new DateTimeOffset(localMidnight, tz.GetUtcOffset(localMidnight));
        
        var utcStart = localStart.ToUniversalTime();
        var utcEnd = utcStart.AddDays(1);
        
        return (utcStart, utcEnd);
    }

    private (decimal subTotalPrice, decimal taxes) GetOrderItemTaxes(PhoneCallOrderItem item, PosProduct product)
    {
        decimal taxes = 0;
        decimal price = 0;

        try
        {
            var productTaxes = JsonConvert.DeserializeObject<List<EasyPosResponseTax>>(product.Tax);
            
            var productTax = productTaxes?.FirstOrDefault()?.Value;
            
            price = product.Price * item.Quantity + item.OrderItemModifiers.Sum(x => x.Price * x.Quantity * item.Quantity);

            taxes += productTax.HasValue ? price * (productTax.Value / 100) : 0;
            
            var modifiers = !string.IsNullOrEmpty(product.Modifiers) ? JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(product.Modifiers) : [];

            taxes += modifiers.Sum(modifier => modifier.ModifierProducts.Sum(x => (x?.Price ?? 0) * ((modifier.Taxes?.FirstOrDefault()?.Value ?? 0) / 100)));
            
            return (price, taxes);
        }
        catch (Exception e)
        {
            Log.Warning("Calculate ai order item: {@OrderItem}-{@Product} taxes failed: {@Exception}", item, product, e);
        }
        
        return (price, taxes);
    }

    private (List<PhoneCallOrderItem> orderItems, decimal subTotal, decimal taxes) BuildPosOrderItems(List<(AiDraftItemDto Item, PosProduct Product)> draftMapping)
    {
        decimal taxes = 0;
        decimal subTotal = 0;
        var orderItems = new List<PhoneCallOrderItem>();
        
        foreach (var (aiDraftItem, product) in draftMapping)
        {
            var item = new PhoneCallOrderItem
            {
                ProductId = Convert.ToInt64(product.ProductId),
                Quantity = aiDraftItem.Quantity,
                OriginalPrice = product.Price,
                Price = product.Price,
                OrderItemModifiers = HandleSpecialItems(aiDraftItem, product)
            };
            
            orderItems.Add(item);
            
            var (itemPrice, itemTax) = GetOrderItemTaxes(item, product);
            
            taxes += itemTax;
            subTotal += itemPrice;
        }
        
        Log.Information("Generate order items: {@orderItems}", orderItems);
        
        Log.Warning("Calculate ai order taxes: {Taxes} and subtotal price: {SubTotal}", taxes, subTotal);
            
        return (orderItems, subTotal, taxes);
    }

    private List<PhoneCallOrderItemModifiers> HandleSpecialItems(AiDraftItemDto aiItem, PosProduct product)
    {
        var modifierItems = !string.IsNullOrWhiteSpace(product?.Modifiers) ? JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(product.Modifiers) : [];

        if (modifierItems == null || modifierItems.Count == 0 || aiItem.Modifiers == null || aiItem.Modifiers.Count == 0) return [];
        
        var orderItemModifiers = new List<PhoneCallOrderItemModifiers>();
        var aiItemModifiersLookup = aiItem.Modifiers.ToDictionary(x => Convert.ToInt64(x.Id), x => x.Quantity);
        
        foreach (var modifierItem in modifierItems)
        {
            var items = modifierItem.ModifierProducts.Where(x => aiItem.Modifiers.Select(m => Convert.ToInt64(m.Id)).Contains(x.Id)).Select(x => new PhoneCallOrderItemModifiers
            {
                Price = x.Price,
                Quantity = aiItemModifiersLookup.TryGetValue(x.Id, out var quantity) ? quantity : 0,
                ModifierId = modifierItem.Id,
                ModifierProductId = x?.Id ?? 0,
                Localizations = _mapper.Map<List<PhoneCallOrderItemLocalization>>(modifierItem.Localizations ?? []),
                ModifierLocalizations = _mapper.Map<List<PhoneCallOrderItemModifierLocalization>>(x?.Localizations ?? [])
            });
            
            orderItemModifiers.AddRange(items);
        }
        
        Log.Information("Generate order item: {@Product} modifiers: {@OrderItemModifiers}", product, orderItemModifiers);
        
        return orderItemModifiers;
    }
}