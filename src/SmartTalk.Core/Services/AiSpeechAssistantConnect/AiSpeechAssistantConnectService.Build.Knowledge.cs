using Serilog;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private const string PosMenuProductNameToken = "{POS_菜单_商品名称}";
    private const string PosMenuProductCategoryToken = "{POS_菜单_商品类别}";
    private const string PosMenuProductSpecificationToken = "{POS_菜单_商品规格}";
    private const string PosMenuProductTaxToken = "{POS_菜单_商品税率}";
    private const string PosMenuProductPriceToken = "{POS_菜单_商品价格}";
    private const string PosMenuProductTimeToken = "{POS_菜单_菜单时间}";
    private static readonly string[] PosMenuProductTokens =
    [
        PosMenuProductNameToken,
        PosMenuProductCategoryToken,
        PosMenuProductSpecificationToken,
        PosMenuProductTaxToken,
        PosMenuProductPriceToken,
        PosMenuProductTimeToken
    ];

    private async Task BuildKnowledgeAsync(CancellationToken cancellationToken)
    {
        await LoadAssistantInfoAsync(cancellationToken).ConfigureAwait(false);
        
        ResolveStaticPromptVariables();
        
        await ResolveGreetingAsync(cancellationToken).ConfigureAwait(false);
        await ResolveCustomerItemsAsync(cancellationToken).ConfigureAwait(false);
        await ResolveMenuItemsAsync(cancellationToken).ConfigureAwait(false);
        await ResolveCustomerInfoAsync(cancellationToken).ConfigureAwait(false);
        await ResolveDeliveryInfoAsync(cancellationToken).ConfigureAwait(false);
        await ResolvePosPromptVariablesAsync(cancellationToken).ConfigureAwait(false);

        Log.Information("[AiAssistant] Prompt resolved, Prompt: {Prompt}", _ctx.Prompt);
    }

    private async Task LoadAssistantInfoAsync(CancellationToken cancellationToken)
    {
        var (assistant, knowledge, userProfile) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync(_ctx.From, _ctx.To, _ctx.ForwardAssistantId ?? _ctx.AssistantId, cancellationToken).ConfigureAwait(false);

        Log.Information("[AiAssistant] Assistant matched, AssistantId: {AssistantId}, HasProfile: {HasProfile}, From: {From}, To: {To}", assistant?.Id, userProfile != null, _ctx.From, _ctx.To);

        _ctx.Assistant = _mapper.Map<AiSpeechAssistantDto>(assistant);
        _ctx.Knowledge = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge);
        
        _ctx.Prompt = _ctx.Knowledge.Prompt;
        _ctx.UserProfileJson = userProfile?.ProfileJson;
    }

    private void ResolveStaticPromptVariables()
    {
        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));

        _ctx.Prompt = _ctx.Prompt
            .Replace("#{user_profile}", string.IsNullOrEmpty(_ctx.UserProfileJson) ? " " : _ctx.UserProfileJson)
            .Replace("#{current_time}", pstTime.ToString("yyyy-MM-dd HH:mm:ss"))
            .Replace("#{customer_phone}", _ctx.From.StartsWith("+1") ? _ctx.From[2..] : _ctx.From)
            .Replace("#{pst_date}", $"{pstTime.Date:yyyy-MM-dd} {pstTime.DayOfWeek}");
    }

    private async Task ResolveGreetingAsync(CancellationToken cancellationToken)
    {
        if (!_ctx.NumberId.HasValue || !_ctx.Prompt.Contains("#{greeting}")) return;

        var greeting = await _smartiesClient
            .GetSaleAutoCallNumberAsync(new GetSaleAutoCallNumberRequest { Id = _ctx.NumberId.Value }, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(greeting.Data.Number.Greeting))
            _ctx.Knowledge.Greetings = greeting.Data.Number.Greeting;
        
        _ctx.Prompt = _ctx.Prompt.Replace("#{greeting}", _ctx.Knowledge.Greetings ?? string.Empty);
    }

    private async Task ResolveCustomerItemsAsync(CancellationToken cancellationToken)
    {
        var hasCustomerItemsToken = _ctx.Prompt.Contains("#{customer_items}", StringComparison.OrdinalIgnoreCase);
        
        var hasHiFoodItemsToken = _ctx.Prompt.Contains("{HiFood_商品_商品数据}", StringComparison.OrdinalIgnoreCase);
        
        if (!hasCustomerItemsToken && !hasHiFoodItemsToken) return;
        
        if (string.IsNullOrWhiteSpace(_ctx.Assistant.Name)) return;

        var caches = await _salesDataProvider.GetCustomerItemsCacheByAssistantNameAsync(_ctx.Assistant.Name, cancellationToken).ConfigureAwait(false);
        var customerItems = caches.Where(c => !string.IsNullOrEmpty(c.CacheValue)).Select(c => c.CacheValue.Trim()).Distinct().ToList();

        var value = customerItems.Count > 0
            ? string.Join(Environment.NewLine + Environment.NewLine, customerItems.Take(50))
            : " ";

        _ctx.Prompt = _ctx.Prompt.Replace("#{customer_items}", value).Replace("{HiFood_商品_商品数据}", value);
    }

    private async Task ResolveMenuItemsAsync(CancellationToken cancellationToken)
    {
        if (!_ctx.Prompt.Contains("#{menu_items}", StringComparison.OrdinalIgnoreCase)) return;

        var menuItems = await GenerateMenuItemsAsync(cancellationToken).ConfigureAwait(false);

        _ctx.Prompt = _ctx.Prompt.Replace("#{menu_items}", menuItems ?? "");
    }

    private async Task ResolveCustomerInfoAsync(CancellationToken cancellationToken)
    {
        var hasCustomerInfoToken = _ctx.Prompt.Contains("#{customer_info}", StringComparison.OrdinalIgnoreCase);
        
        var hasCrmCustomerToken = _ctx.Prompt.Contains("{CRM_客户_客户数据}", StringComparison.OrdinalIgnoreCase);
        
        if (!hasCustomerInfoToken && !hasCrmCustomerToken) return;

        var cache = await _salesDataProvider.GetCustomerInfoCacheByPhoneNumberAsync(_ctx.From, cancellationToken).ConfigureAwait(false);

        var value = cache?.CacheValue?.Trim() ?? " ";

        _ctx.Prompt = _ctx.Prompt
            .Replace("#{customer_info}", value)
            .Replace("{CRM_客户_客户数据}", value);
    }

    private async Task ResolveDeliveryInfoAsync(CancellationToken cancellationToken)
    {
        if (!_ctx.Prompt.Contains("#{delivery_info}", StringComparison.OrdinalIgnoreCase) && !_ctx.Prompt.Contains("{CRM_路线_送货日数据}", StringComparison.OrdinalIgnoreCase)) return;

        var cache = await _salesDataProvider.GetDeliveryInfoCacheByPhoneNumberAsync(_ctx.From, cancellationToken).ConfigureAwait(false);
        _ctx.Prompt = _ctx.Prompt.Replace("#{delivery_info}", cache?.CacheValue?.Trim() ?? " ").Replace("{CRM_路线_送货日数据}", cache?.CacheValue?.Trim() ?? " ");
    }

    private async Task ResolvePosPromptVariablesAsync(CancellationToken cancellationToken)
    {
        var requestedProductTokens = GetRequestedPosProductTokens();
        var needProducts = requestedProductTokens.Count > 0;
        var needStoreHours = HasPromptToken("{POS_店铺信息_营业时间}");

        if (!needProducts && !needStoreHours) return;

        var language = ResolvePosPromptLanguage();
        var products = needProducts
            ? await _posUtilService.GetPosMenuProductBriefsAsync(_ctx.AgentId, language, cancellationToken).ConfigureAwait(false)
            : [];

        if (requestedProductTokens.Count > 2)
        {
            ResolveCompactPosProductData(products, requestedProductTokens);
        }
        else
        {
            ResolvePosMenuProductNames(products);
            ResolvePosMenuProductCategories(products);
            ResolvePosMenuProductSpecifications(products);
            ResolvePosMenuProductTaxes(products);
            ResolvePosMenuProductPrices(products);
            ResolvePosMenuProductTimes(products);
        }

        if (needStoreHours)
        {
            var storeHours = await _posUtilService.GetPosStoreTimePeriodsAsync(_ctx.AgentId, language, cancellationToken).ConfigureAwait(false);
            ResolvePosStoreBusinessHours(storeHours);
        }
    }

    private void ResolvePosMenuProductNames(List<PosMenuProductBriefDto> products)
    {
        var value = string.Join("、", products
            .Select(BuildProductDisplayName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal));

        ReplacePromptToken("{POS_菜单_商品名称}", value);
    }

    private void ResolvePosMenuProductCategories(List<PosMenuProductBriefDto> products)
    {
        var lines = products
            .Select(x => new { ProductName = BuildProductDisplayName(x), x.CategoryName })
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductName))
            .GroupBy(x => x.ProductName.Trim(), StringComparer.Ordinal)
            .Select(group =>
            {
                var categories = group
                    .Select(x => x.CategoryName?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (categories.Count == 0) return null;

                return $"{group.Key}：{string.Join("、", categories)}";
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var value = string.Join(Environment.NewLine, lines);

        ReplacePromptToken("{POS_菜单_商品类别}", value);
    }

    private void ResolvePosMenuProductSpecifications(List<PosMenuProductBriefDto> products)
    {
        var lines = products
            .Select(x => new { ProductName = BuildProductDisplayName(x), x.Specification })
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductName) && !string.IsNullOrWhiteSpace(x.Specification))
            .Select(x => $"{x.ProductName}：{x.Specification}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        ReplacePromptToken("{POS_菜单_商品规格}", string.Join(Environment.NewLine, lines));
    }

    private void ResolvePosMenuProductTaxes(List<PosMenuProductBriefDto> products)
    {
        var lines = products
            .Select(x => new { ProductName = BuildProductDisplayName(x), x.Tax })
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductName) && !string.IsNullOrWhiteSpace(x.Tax))
            .Select(x => $"{x.ProductName}：{x.Tax}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        ReplacePromptToken("{POS_菜单_商品税率}", string.Join(Environment.NewLine, lines));
    }

    private void ResolvePosMenuProductPrices(List<PosMenuProductBriefDto> products)
    {
        var lines = products
            .Select(x => new { ProductName = BuildProductDisplayName(x), x.Price })
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductName))
            .Select(x => $"{x.ProductName}：{x.Price:F2}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        ReplacePromptToken("{POS_菜单_商品价格}", string.Join(Environment.NewLine, lines));
    }

    private void ResolvePosMenuProductTimes(List<PosMenuProductBriefDto> products)
    {
        var lines = products
            .Select(x => new { ProductName = BuildProductDisplayName(x), x.PosMenus })
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductName) && x.PosMenus is { Count: > 0 })
            .Select(x =>
            {
                var periods = x.PosMenus
                    .Select(menu =>
                    {
                        var name = menu?.Name?.Trim() ?? string.Empty;
                        var time = menu?.TimePeriod?.Trim() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(name)) return time;
                        if (string.IsNullOrWhiteSpace(time)) return name;

                        return $"{name}({time})";
                    })
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (periods.Count == 0) return null;

                return $"{x.ProductName}：{string.Join("；", periods)}";
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        ReplacePromptToken("{POS_菜单_菜单时间}", string.Join(Environment.NewLine, lines));
    }

    private void ResolvePosStoreBusinessHours(string storeHours) => ReplacePromptToken("{POS_店铺信息_营业时间}", storeHours);

    private List<string> GetRequestedPosProductTokens()
    {
        return PosMenuProductTokens
            .Select(token => new { Token = token, Index = _ctx.Prompt.IndexOf(token, StringComparison.OrdinalIgnoreCase) })
            .Where(x => x.Index >= 0)
            .OrderBy(x => x.Index)
            .Select(x => x.Token)
            .ToList();
    }

    private void ResolveCompactPosProductData(List<PosMenuProductBriefDto> products, List<string> requestedTokens)
    {
        var compactValue = BuildCompactPosProductData(products, requestedTokens);

        if (string.IsNullOrWhiteSpace(compactValue))
        {
            foreach (var token in requestedTokens)
                ReplacePromptToken(token, " ");

            return;
        }

        ReplacePromptToken(requestedTokens[0], compactValue);

        foreach (var token in requestedTokens.Skip(1))
            ReplacePromptToken(token, "见 POS 商品索引与详情。");
    }

    private string BuildCompactPosProductData(List<PosMenuProductBriefDto> products, List<string> requestedTokens)
    {
        var entries = BuildCompactPosProductEntries(products);

        if (entries.Count == 0) return " ";

        var indexLines = entries
            .Select(entry => $"{entry.Id}={entry.ProductName}")
            .ToList();

        var detailLines = entries
            .Select(entry => BuildCompactPosProductDetailLine(entry, requestedTokens))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var requestedFields = string.Join("、", requestedTokens
            .Select(GetPosPromptFieldLabel)
            .Distinct(StringComparer.Ordinal));

        return string.Join(Environment.NewLine, new[]
        {
            $"POS商品紧凑数据（覆盖字段：{requestedFields}；编号仅用于内部关联，回答用户时请使用商品名称）",
            "商品索引:",
            string.Join(Environment.NewLine, indexLines),
            "商品详情:",
            string.Join(Environment.NewLine, detailLines)
        });
    }

    private List<PosProductPromptEntry> BuildCompactPosProductEntries(List<PosMenuProductBriefDto> products)
    {
        return products
            .Select((product, index) => new
            {
                Product = product,
                Index = index,
                ProductName = BuildProductDisplayName(product).Trim()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductName))
            .GroupBy(x => x.ProductName, StringComparer.Ordinal)
            .OrderBy(group => group.Min(x => x.Index))
            .Select((group, index) => new PosProductPromptEntry(
                $"P{index + 1:000}",
                group.Key,
                group.Select(x => x.Product.CategoryName?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                group.Select(x => x.Product.Specification?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                group.Select(x => x.Product.Tax?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                group.Select(x => x.Product.Price.ToString("F2"))
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                group.SelectMany(x => x.Product.PosMenus ?? [])
                    .Select(BuildPosMenuPeriod)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .ToList()))
            .ToList();
    }

    private string BuildCompactPosProductDetailLine(PosProductPromptEntry entry, List<string> requestedTokens)
    {
        var segments = new List<string> { entry.Id };

        if (requestedTokens.Contains(PosMenuProductCategoryToken) && entry.Categories.Count > 0)
            segments.Add($"类别={string.Join("、", entry.Categories)}");

        if (requestedTokens.Contains(PosMenuProductSpecificationToken) && entry.Specifications.Count > 0)
            segments.Add($"规格={string.Join("、", entry.Specifications)}");

        if (requestedTokens.Contains(PosMenuProductTaxToken) && entry.Taxes.Count > 0)
            segments.Add($"税率={string.Join("、", entry.Taxes)}");

        if (requestedTokens.Contains(PosMenuProductPriceToken) && entry.Prices.Count > 0)
            segments.Add($"价格={string.Join("、", entry.Prices)}");

        if (requestedTokens.Contains(PosMenuProductTimeToken) && entry.MenuTimes.Count > 0)
            segments.Add($"菜单时间={string.Join("；", entry.MenuTimes)}");

        return string.Join(" | ", segments);
    }

    private static string GetPosPromptFieldLabel(string token)
    {
        return token switch
        {
            PosMenuProductNameToken => "商品名称",
            PosMenuProductCategoryToken => "商品类别",
            PosMenuProductSpecificationToken => "商品规格",
            PosMenuProductTaxToken => "商品税率",
            PosMenuProductPriceToken => "商品价格",
            PosMenuProductTimeToken => "菜单时间",
            _ => token
        };
    }

    private bool HasPromptToken(string token) => _ctx.Prompt.Contains(token, StringComparison.OrdinalIgnoreCase);

    private void ReplacePromptToken(string token, string value)
    {
        if (!HasPromptToken(token)) return;

        _ctx.Prompt = _ctx.Prompt.Replace(token, string.IsNullOrWhiteSpace(value) ? " " : value);
    }

    private static string BuildProductDisplayName(PosMenuProductBriefDto product)
    {
        var cn = product?.NameCn?.Trim() ?? string.Empty;
        var en = product?.NameEn?.Trim() ?? string.Empty;
        var fallback = product?.Name?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(cn) && !string.IsNullOrWhiteSpace(en))
            return string.Equals(cn, en, StringComparison.OrdinalIgnoreCase) ? cn : $"{cn} ({en})";

        if (!string.IsNullOrWhiteSpace(cn))
            return cn;

        if (!string.IsNullOrWhiteSpace(en))
            return en;

        return fallback;
    }

    private static string BuildPosMenuPeriod(PosMenuBriefDto menu)
    {
        var name = menu?.Name?.Trim() ?? string.Empty;
        var time = menu?.TimePeriod?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name)) return time;
        if (string.IsNullOrWhiteSpace(time)) return name;

        return $"{name}({time})";
    }

    private TranscriptionLanguage ResolvePosPromptLanguage()
    {
        Enum.TryParse(_ctx.Assistant?.ModelLanguage, true, out AiSpeechAssistantMainLanguage language);
        language = language == default ? AiSpeechAssistantMainLanguage.En : language;

        return language switch
        {
            AiSpeechAssistantMainLanguage.Zh => TranscriptionLanguage.Chinese,
            AiSpeechAssistantMainLanguage.Cantonese => TranscriptionLanguage.Chinese,
            AiSpeechAssistantMainLanguage.Spanish => TranscriptionLanguage.Spanish,
            AiSpeechAssistantMainLanguage.Korean => TranscriptionLanguage.Korean,
            _ => TranscriptionLanguage.English
        };
    }

    private sealed record PosProductPromptEntry(
        string Id,
        string ProductName,
        List<string> Categories,
        List<string> Specifications,
        List<string> Taxes,
        List<string> Prices,
        List<string> MenuTimes);
}
