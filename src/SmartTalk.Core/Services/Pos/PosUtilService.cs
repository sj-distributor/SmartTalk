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
    
    Task<decimal> CalculateOrderAmountAsync(AiSpeechAssistantDto assistant, BinaryData audioData, CancellationToken cancellationToken = default);

    Task<List<PosMenuProductBriefDto>> GetPosMenuProductBriefsAsync(int agentId, TranscriptionLanguage language = TranscriptionLanguage.Chinese, CancellationToken cancellationToken = default);

    Task<string> GetPosMenuTimePeriodsAsync(int agentId, TranscriptionLanguage language = TranscriptionLanguage.Chinese, CancellationToken cancellationToken = default);

    Task<string> GetPosStoreTimePeriodsAsync(int agentId, TranscriptionLanguage language = TranscriptionLanguage.Chinese, CancellationToken cancellationToken = default);
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
        if (IsNotOrderScenario(record)) return;
        
        if (agent.Type != AgentType.PosCompanyStore || !assistant.IsAutoGenerateOrder) return;
        
        if (await CheckPosOrderIfExistsAsync(record.Id, cancellationToken).ConfigureAwait(false)) return;
        
        var report = await GetOriginalAnalysisReportAsync(record, cancellationToken).ConfigureAwait(false);
        
        var (products, menuItems) = await GeneratePosMenuItemsAsync(agent.Id, true, record.Language, cancellationToken).ConfigureAwait(false);

        try
        {
            var aiDraftOrder = await GenerateDraftOrderFromReportAsync(record.Language, record.IncomingCallNumber, menuItems, report, cancellationToken).ConfigureAwait(false);

            var matchedProducts = MatchPosProducts(products, aiDraftOrder);

            await HandleAiDraftOrderSpecificationsAsync(record, matchedProducts, aiDraftOrder, cancellationToken).ConfigureAwait(false);

            var order = await BuildPosOrderAsync(record, aiDraftOrder, matchedProducts, cancellationToken).ConfigureAwait(false);

            if (assistant.IsAllowOrderPush)
                await _posService.HandlePosOrderAsync(order, false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error(e, "Generate ai draft order failed");
        }
    }

    private bool IsNotOrderScenario(PhoneOrderRecord record)
    {
        if (record is { Scenario: DialogueScenarios.Order }) return false;
        
        Log.Information("The scenario is not the order scenario: {@Record}.", record);
        
        return true;
    }

    private async Task<bool> CheckPosOrderIfExistsAsync(int recordId, CancellationToken cancellationToken)
    {
        var posOrder = await _posDataProvider.GetPosOrderByIdAsync(recordId: recordId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (posOrder == null) return false;
        
        Log.Information("The order already exist: {@PosOrder}, recordId: {RecordId}", posOrder, recordId);
            
        return true;
    }

    private async Task<string> GetOriginalAnalysisReportAsync(PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        var originalReport = await _phoneOrderDataProvider.GetOriginalPhoneOrderRecordReportAsync(record.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

        return originalReport?.Report ?? record.TranscriptionText;
    }

    private async Task<AiDraftOrderDto> GenerateDraftOrderFromReportAsync(TranscriptionLanguage language, string incomingCallNumber, string menuItems, string report, CancellationToken cancellationToken)
    {
        var client = new ChatClient("gpt-4.1", _openAiSettings.ApiKey);

        var systemPrompt =
            "你是一名訂單分析助手。請從下面的客戶分析報告文字中提取客人的姓名、电话、配送类型以及配送地址，以及所有下單的菜品、數量、規格、备注，並且用菜單列表盡力匹配每個菜品。\n" +
            "如果報告中提到了送餐類型，請提取送餐類型 type (0: 自提订单，1：配送订单)。\n" +
            "如果客户有要求或者提供其他的号码作为订单的号码，請提取客户的电话 phoneNumber ，否则 phoneNumber 为当前的来电号码：" + incomingCallNumber + "。\n"+
            "如果報告中提到了客户的姓名，請提取客户的姓名 customerName 。\n" +
            "如果報告中提到了客户的配送地址，請提取客户的配送地址 customerAddress，若无则忽略 。\n" +
            "如果報告中提到了客户的订单注意事项或者是要求，且該內容不能獨立構成一個可下單的菜品名稱，則請提取為备注信息 notes；若该要求是附属于某一道菜品的特殊交代（如口味、加料、忌口），則在不影響該菜品正常生成 items 的前提下，將該要求體現在 notes 中。\n" +
            "另外请注意备注的语言与表达方式：当前语言为 " + language.GetDescription() + "，zh 用中文，否则用英文；备注必须为客户原话或直接指令（如“不要辣”、“rice on the side”），去除“客人/Customer”等前缀，不得使用第三人称转述。\n" +
            "請嚴格傳回一個 JSON 對象，頂層字段為 \"type\"，items（数组，元素包含 productId：菜品ID, name：菜品名, quantity：数量, specification：规格（比如：大、中、小，加小料、加椰果或者有关菜品的其他内容））。\n" +
            "範例：\n" +
            "{\"type\":0,\"phoneNumber\":\"40085235698\",\"customerName\":\"刘先生\",\"customerAddress\":\"中环广场一座\",\"notes\":\"给个酱油包\",\"items\":[{\"productId\":\"9778779965031491\",\"name\":\"海南雞湯麵\",\"quantity\":1,\"specification\":null}]}" +
            "{\"type\":1,\"phoneNumber\":\"40026235458\",\"customerName\":\"吴先生\",\"customerAddress\":\"中环广场三座\",\"notes\":\"到了不要敲门，放门口\",\"items\":[{\"productId\":\"9225097809167409\",\"name\":\"港式燒鴨\",\"quantity\":1,\"specification\":\"半隻\"}]} \n\n" +
            "菜單列表：\n" + menuItems + "\n\n" +
            "菜品提取補充規則（非常重要）：\n" +
            "1. 菜單中的序號（如 1.、144.）不是 productId 的一部分，必須忽略。\n" +
            "2. 每一行菜單必須拆分為：菜名 + (數字ID)，只允許提取括號中的數字作為 productId。\n" +
            "3. productId 必須為純數字字符串（例如：\"9593707405182106\"）。\n" +
            "4. 嚴禁將“序號.菜名”或“序號.菜名(數字ID)”或任何包含文字的內容作為 productId。\n" +
            "5. 如果 productId 包含非數字內容（如字母、點號、菜名），則該結果視為錯誤，不允許輸出。\n" +
            "6. productId 必須能在菜單中唯一對應到某一個菜品，否則不得輸出該 item。\n" +
            "5. 如果客戶對飯或湯有修改（如不要湯、換飯），需體現在 specification 或 notes 中。\n" +
            "注意：\n1. 必須嚴格按格式輸出 JSON，不要有其他字段或額外說明。\n2. **如果客戶分析文本中沒有任何可識別的下單信息，請返回：{ \"type\":0, \"items\": [] }。不得臆造或猜測菜品。** \n" +
            "請務必完整提取報告中每一個提到的菜品";

        Log.Information("Sending prompt with menu items to GPT: {Prompt}", systemPrompt);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage("客戶分析報告文本：\n" + report + "\n\n")
        };

        var completion = await client.CompleteChatAsync(messages, new ChatCompletionOptions
        {
            ResponseModalities = ChatResponseModalities.Text,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        }, cancellationToken).ConfigureAwait(false);
        
        var aiDraftOrder = JsonConvert.DeserializeObject<AiDraftOrderDto>(completion.Value.Content.FirstOrDefault()?.Text ?? "");

        Log.Information("Deserialize response to ai order: {@AiOrder}", aiDraftOrder);
        
        return aiDraftOrder;
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

    private List<PosProduct> MatchPosProducts(List<PosProduct> products, AiDraftOrderDto aiDraftOrder)
    {
        var matchedProducts = products.Where(x => aiDraftOrder.Items.Select(p => p.ProductId).Contains(x.ProductId)).DistinctBy(x => x.ProductId).ToList();

        Log.Information("Matched products: {@MatchedProducts}", matchedProducts);
        
        return matchedProducts;
    }

    private async Task HandleAiDraftOrderSpecificationsAsync(PhoneOrderRecord record, List<PosProduct> matchedProducts, AiDraftOrderDto aiDraftOrder, CancellationToken cancellationToken)
    {
        var productModifiersLookup = matchedProducts.Where(x => !string.IsNullOrWhiteSpace(x.Modifiers))
            .ToDictionary(x => x.ProductId, x => JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(x.Modifiers));

        Log.Information("Build product's modifiers: {@ModifiersLookUp}", productModifiersLookup);

        foreach (var aiDraftItem in aiDraftOrder.Items.Where(x => !string.IsNullOrEmpty(x.Specification) && !string.IsNullOrEmpty(x.ProductId)))
        {
            if (productModifiersLookup.TryGetValue(aiDraftItem.ProductId, out var modifiers))
            {
                try
                {
                    var builtModifiers = await GenerateSpecificationProductsAsync(modifiers, record.Language, aiDraftItem.Specification, cancellationToken).ConfigureAwait(false);
                        
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

    public async Task<List<PosMenuProductBriefDto>> GetPosMenuProductBriefsAsync(int agentId, TranscriptionLanguage language = TranscriptionLanguage.Chinese, CancellationToken cancellationToken = default)
    {
        var productMenuPairs = await _posDataProvider
            .GetPosProductsWithMenusByAgentIdAsync(agentId, cancellationToken)
            .ConfigureAwait(false);

        if (productMenuPairs.Count == 0) return [];

        var productsWithMenus = productMenuPairs
            .Where(x => x.Product is { Status: true } && !string.IsNullOrWhiteSpace(x.Product.ProductId))
            .GroupBy(x => x.Product.ProductId)
            .Select(group =>
            {
                var product = group
                    .Select(x => x.Product)
                    .OrderBy(x => x.SortOrder ?? int.MaxValue)
                    .ThenBy(x => x.Id)
                    .First();

                var menus = group
                    .Select(x => x.Menu)
                    .Where(x => x != null)
                    .DistinctBy(x => x.Id)
                    .OrderBy(x => x.Id)
                    .ToList();

                return (Product: product, Menus: menus);
            })
            .OrderBy(x => x.Product.SortOrder ?? int.MaxValue)
            .ThenBy(x => x.Product.ProductId ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        var categories = await _posDataProvider.GetPosCategoriesAsync(
            ids: productsWithMenus.Select(x => x.Product.CategoryId).Distinct().ToList(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var categoryNamesById = categories
            .DistinctBy(x => x.Id)
            .ToDictionary(
                x => x.Id,
                x =>
                {
                    try
                    {
                        var localization = JsonConvert.DeserializeObject<PosNamesLocalization>(x.Names);
                        return BuildMenuItemPosName(localization, language);
                    }
                    catch
                    {
                        return string.Empty;
                    }
                });

        var result = new List<PosMenuProductBriefDto>(productsWithMenus.Count);

        foreach (var (product, menus) in productsWithMenus)
        {
            var productLocalization = JsonConvert.DeserializeObject<PosNamesLocalization>(product.Names);
            var name = BuildMenuItemPosName(productLocalization, language);
            var categoryName = categoryNamesById.TryGetValue(product.CategoryId, out var currentCategoryName) ? currentCategoryName : string.Empty;

            var taxes = ParseProductTaxes(product.Tax);
            var specification = ParseProductModifierOptions(product.Modifiers, language);
            var modifierGroups = ParseProductModifierGroups(product.Modifiers, language);
            var menuInfos = menus
                .Select(menu =>
                {
                    string menuName;
                    try
                    {
                        var localization = JsonConvert.DeserializeObject<PosNamesLocalization>(menu.Names);
                        menuName = BuildMenuItemPosName(localization, language);
                    }
                    catch
                    {
                        menuName = string.Empty;
                    }

                    var timePeriod = ParseMenuTimePeriods(menu.TimePeriod, language);

                    if (string.IsNullOrWhiteSpace(menuName) && string.IsNullOrWhiteSpace(timePeriod))
                        return null;

                    return new PosMenuBriefDto
                    {
                        Name = menuName,
                        TimePeriod = timePeriod
                    };
                })
                .Where(x => x != null)
                .DistinctBy(x => new { x.Name, x.TimePeriod })
                .ToList();

            result.Add(new PosMenuProductBriefDto
            {
                Name = name,
                CategoryName = categoryName,
                Price = product.Price,
                Tax = taxes,
                Specification = specification,
                ModifierGroups = modifierGroups,
                PosMenus = menuInfos
            });
        }

        return result;
    }

    public async Task<string> GetPosMenuTimePeriodsAsync(int agentId, TranscriptionLanguage language = TranscriptionLanguage.Chinese, CancellationToken cancellationToken = default)
    {
        var storeAgent = await _posDataProvider.GetPosAgentByAgentIdAsync(agentId, cancellationToken).ConfigureAwait(false);
        if (storeAgent == null) return string.Empty;

        var menus = await _posDataProvider.GetPosMenusAsync(storeAgent.StoreId, true, cancellationToken).ConfigureAwait(false);
        if (menus == null || menus.Count == 0) return string.Empty;

        var lines = new List<string>();

        foreach (var menu in menus)
        {
            if (string.IsNullOrWhiteSpace(menu?.TimePeriod)) continue;

            List<EasyPosResponseTimePeriod> periods;
            try
            {
                periods = JsonConvert.DeserializeObject<List<EasyPosResponseTimePeriod>>(menu.TimePeriod);
            }
            catch
            {
                continue;
            }

            if (periods == null || periods.Count == 0) continue;

            var menuName = string.Empty;
            try
            {
                var localization = JsonConvert.DeserializeObject<PosNamesLocalization>(menu.Names);
                menuName = BuildMenuItemPosName(localization, language);
            }
            catch
            {
                menuName = string.Empty;
            }

            foreach (var period in periods)
            {
                if (period == null || string.IsNullOrWhiteSpace(period.StartTime) || string.IsNullOrWhiteSpace(period.EndTime))
                    continue;

                var dayText = BuildDayOfWeekSummary(period.DayOfWeeks, language);
                var periodName = string.IsNullOrWhiteSpace(period.Name) ? string.Empty : period.Name.Trim();
                var label = string.IsNullOrWhiteSpace(menuName) ? string.Empty : $"{menuName} ";
                var window = $"{period.StartTime}-{period.EndTime}";

                var line = string.IsNullOrWhiteSpace(periodName)
                    ? $"{label}{dayText} {window}".Trim()
                    : $"{label}{periodName} {dayText} {window}".Trim();

                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }
        }

        var distinctLines = lines
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return distinctLines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, distinctLines);
    }

    public async Task<string> GetPosStoreTimePeriodsAsync(int agentId, TranscriptionLanguage language = TranscriptionLanguage.Chinese, CancellationToken cancellationToken = default)
    {
        var store = await _posDataProvider.GetPosStoreByAgentIdAsync(agentId, cancellationToken).ConfigureAwait(false);
        if (store == null || string.IsNullOrWhiteSpace(store.TimePeriod)) return string.Empty;

        List<StoreTimePeriod> periods;
        try
        {
            periods = JsonConvert.DeserializeObject<List<StoreTimePeriod>>(store.TimePeriod);
        }
        catch
        {
            return string.Empty;
        }

        if (periods == null || periods.Count == 0) return string.Empty;

        var lines = new List<string>();

        foreach (var period in periods)
        {
            if (period == null || string.IsNullOrWhiteSpace(period.StartTime) || string.IsNullOrWhiteSpace(period.EndTime))
                continue;

            var dayText = BuildDayOfWeekSummary(period.DayOfWeeks, language);
            var periodName = string.IsNullOrWhiteSpace(period.Name) ? string.Empty : period.Name.Trim();
            var window = $"{period.StartTime}-{period.EndTime}";

            var line = string.IsNullOrWhiteSpace(periodName)
                ? $"{dayText} {window}".Trim()
                : $"{periodName} {dayText} {window}".Trim();

            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }

        var distinctLines = lines
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return distinctLines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, distinctLines);
    }

    private static string BuildDayOfWeekSummary(List<int> dayOfWeeks, TranscriptionLanguage language)
    {
        if (dayOfWeeks == null || dayOfWeeks.Count == 0) return language == TranscriptionLanguage.English ? "Daily" : "每天";

        var validDays = dayOfWeeks
            .Where(x => x is >= 0 and <= 6)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (validDays.Count == 0 || validDays.Count == 7) return language == TranscriptionLanguage.English ? "Daily" : "每天";

        var zhDays = new[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
        var enDays = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        var dayNames = validDays.Select(x => language == TranscriptionLanguage.English ? enDays[x] : zhDays[x]);

        return language == TranscriptionLanguage.English
            ? string.Join(",", dayNames)
            : string.Join("、", dayNames);
    }

    private string BuildMenuItemPosName(PosNamesLocalization localization, TranscriptionLanguage language = TranscriptionLanguage.Chinese)
    {
        if (language is TranscriptionLanguage.Chinese)
        {
            var zhPosName = !string.IsNullOrWhiteSpace(localization?.Cn?.PosName) ? localization.Cn.PosName : string.Empty;
            if (!string.IsNullOrWhiteSpace(zhPosName)) return zhPosName;

            var zhName = !string.IsNullOrWhiteSpace(localization?.Cn?.Name) ? localization.Cn.Name : string.Empty;
            if (!string.IsNullOrWhiteSpace(zhName)) return zhName;

            var zhSendChefName = !string.IsNullOrWhiteSpace(localization?.Cn?.SendChefName) ? localization.Cn.SendChefName : string.Empty;
            if (!string.IsNullOrWhiteSpace(zhSendChefName)) return zhSendChefName;
        }

        var usPosName = !string.IsNullOrWhiteSpace(localization?.En?.PosName) ? localization.En.PosName : string.Empty;
        if (!string.IsNullOrWhiteSpace(usPosName)) return usPosName;

        var usName = !string.IsNullOrWhiteSpace(localization?.En?.Name) ? localization.En.Name : string.Empty;
        if (!string.IsNullOrWhiteSpace(usName)) return usName;

        var usSendChefName = !string.IsNullOrWhiteSpace(localization?.En?.SendChefName) ? localization.En.SendChefName : string.Empty;
        if (!string.IsNullOrWhiteSpace(usSendChefName)) return usSendChefName;

        return string.Empty;
    }

    private static string ParseProductTaxes(string taxJson)
    {
        if (string.IsNullOrWhiteSpace(taxJson)) return string.Empty;

        try
        {
            var taxes = JsonConvert.DeserializeObject<List<EasyPosResponseTax>>(taxJson);
            if (taxes == null || taxes.Count == 0) return string.Empty;

            var simplified = taxes
                .Select(t => t == null
                    ? null
                    : t.IsPercentage
                        ? $"{t.Value:0.##}%"
                        : $"{t.Value:0.##}")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return simplified.Count == 0 ? string.Empty : string.Join(" + ", simplified);
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ParseProductModifierOptions(string modifiersJson, TranscriptionLanguage language)
    {
        if (string.IsNullOrWhiteSpace(modifiersJson) || modifiersJson == "[]") return string.Empty;

        try
        {
            var groups = JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(modifiersJson);
            if (groups == null || groups.Count == 0) return string.Empty;

            var groupSummaries = new List<string>();

            foreach (var group in groups)
            {
                if (group?.ModifierProducts == null || group.ModifierProducts.Count == 0) continue;

                var options = group.ModifierProducts
                    .Select(x =>
                    {
                        var optionName = BuildModifierName(x.Localizations, language);
                        if (string.IsNullOrWhiteSpace(optionName)) return null;

                        return $"{optionName}(+{x.Price:0.##})";
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (options.Count == 0) continue;

                var optionsText = string.Join("/", options);

                var groupName = BuildModifierName(group.Localizations, language);
                if (string.IsNullOrWhiteSpace(groupName) || groups.Count == 1)
                    groupSummaries.Add(optionsText);
                else
                    groupSummaries.Add($"{groupName}:{optionsText}");
            }

            if (groupSummaries.Count == 0) return string.Empty;

            return string.Join("；", groupSummaries.Distinct(StringComparer.Ordinal));
        }
        catch
        {
            return string.Empty;
        }
    }

    private List<PosMenuProductModifierGroupDto> ParseProductModifierGroups(string modifiersJson, TranscriptionLanguage language)
    {
        if (string.IsNullOrWhiteSpace(modifiersJson) || modifiersJson == "[]") return [];

        try
        {
            var groups = JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(modifiersJson);
            if (groups == null || groups.Count == 0) return [];

            return groups
                .Where(x => x?.ModifierProducts != null && x.ModifierProducts.Count != 0)
                .Select(group => new PosMenuProductModifierGroupDto
                {
                    Id = group.Id,
                    Name = BuildModifierName(group.Localizations, language),
                    MinimumSelect = group.MinimumSelect,
                    MaximumSelect = group.MaximumSelect,
                    MaximumRepetition = group.MaximumRepetition,
                    Options = group.ModifierProducts
                        .Select(option => new PosMenuProductModifierOptionDto
                        {
                            Id = option.Id,
                            ProductId = option.ProductId,
                            Name = BuildModifierName(option.Localizations, language),
                            Price = option.Price
                        })
                        .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                        .DistinctBy(x => x.Id)
                        .ToList()
                })
                .Where(x => x.Options.Count != 0)
                .DistinctBy(x => x.Id)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private string ParseMenuTimePeriods(string timePeriodJson, TranscriptionLanguage language)
    {
        if (string.IsNullOrWhiteSpace(timePeriodJson)) return string.Empty;

        try
        {
            var periods = JsonConvert.DeserializeObject<List<EasyPosResponseTimePeriod>>(timePeriodJson);
            if (periods == null || periods.Count == 0) return string.Empty;

            var lines = periods
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.StartTime) && !string.IsNullOrWhiteSpace(x.EndTime))
                .Select(x =>
                {
                    var dayText = BuildDayOfWeekSummary(x.DayOfWeeks, language);
                    var periodName = string.IsNullOrWhiteSpace(x.Name) ? string.Empty : x.Name.Trim();
                    var window = $"{x.StartTime}-{x.EndTime}";

                    return string.IsNullOrWhiteSpace(periodName)
                        ? $"{dayText} {window}".Trim()
                        : $"{periodName} {dayText} {window}".Trim();
                })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return lines.Count == 0 ? string.Empty : string.Join("；", lines);
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<decimal> CalculateOrderAmountAsync(
       AiSpeechAssistantDto assistant, BinaryData audioData, CancellationToken cancellationToken = default)
    {
        var agent = await _agentDataProvider.GetAgentByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get agent: {@Agent} by assistant id: {AssistantId}", agent, assistant.Id);
        
        if (agent == null) return 0;

        var language = assistant.ModelLanguage is "Mandarin" or "Cantonese" ? TranscriptionLanguage.Chinese : TranscriptionLanguage.English;

        var report = await GenerateAiDraftReportAsync(agent, audioData, language, cancellationToken).ConfigureAwait(false);
        
        var (products, menuItems) = await GeneratePosMenuItemsAsync(agent.Id, true, language, cancellationToken).ConfigureAwait(false);

        var aiDraftOrder = await GenerateDraftOrderFromReportAsync(language, string.Empty, menuItems, report, cancellationToken).ConfigureAwait(false);

        var matchedProducts = MatchPosProducts(products, aiDraftOrder);
            
        var draftMapping = BuildAiDraftAndProductMapping(matchedProducts, aiDraftOrder.Items);
        
        var (_, subTotal, taxes) = BuildPosOrderItems(draftMapping);

        return subTotal + taxes;
    }

    private async Task<string> GenerateAiDraftReportAsync(Agent agent, BinaryData audioData, TranscriptionLanguage language, CancellationToken cancellationToken)
    {
        var (_, menuItems) = await GeneratePosMenuItemsAsync(agent.Id, false, language, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Build menu items for summary: {@MenuItems}", menuItems);
        
        List<ChatMessage> messages =
        [
            new SystemChatMessage("你是一名電話訂單內容分析員，只需要從錄音內容中提取客戶實際下單的菜品資訊。\n\n" +
                                  "請僅輸出「客戶下單內容」，不要輸出任何其他說明、分析、摘要或多餘文字。\n\n" +
                                  "輸出格式必須嚴格遵守以下結構（若無有效下單內容，則不輸出任何內容）：\n\n" +
                                  "客戶下單內容（請務必按該格式輸出：菜品 數量 規格）：\n" +
                                  "1. 港式奶茶 2杯\n" +
                                  "2. 海南雞湯麵 2份\n" +
                                  "3. 叉燒包派對餐 1份 小份(12pcs)\n\n" +
                                  "判定下單成立的情況包括（但不限於）：\n" +
                                  "- 客戶直接說明要下單某些菜品\n" +
                                  "- 客戶表示需要點幾個菜，並對服務人員或系統推薦的菜品表示明確確認（如「好」、「可以」、「就這些」、「OK」、「行」、「沒問題」等），此時應視為客戶已下單該推薦的菜品\n" +
                                  "- 客戶在推薦後未提出異議，並結束或暫停對話，可視為默認確認推薦內容\n\n" +
                                  "注意事項：\n- 僅提取客戶已確認的下單菜品（包含確認推薦的情況）\n" +
                                  "- 不要推測或補全未被推薦或未被確認的菜品\n" +
                                  "- 規格（如大小、數量、套餐）僅在錄音中明確出現時才輸出\n" +
                                  "- 若客戶未下單或未識別到有效菜品，請不要輸出任何內容\n\n" +
                                  "客戶下單的菜品可能參考以下菜單，但不限於此：" + menuItems),
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
                ProductId = x.ProductId,
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