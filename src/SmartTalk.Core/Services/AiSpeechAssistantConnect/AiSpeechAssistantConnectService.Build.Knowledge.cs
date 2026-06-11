using Serilog;
using SmartTalk.Core.Services.AiSpeechAssistantConnect.Exceptions;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.STT;
using AiSpeechAssistantEntity = SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant;
using AiSpeechAssistantKnowledgeEntity = SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistantKnowledge;

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
        await ResolveDeliveryProgressAsync(cancellationToken).ConfigureAwait(false);
        await ResolveMenuItemsAsync(cancellationToken).ConfigureAwait(false);
        await ResolveCustomerInfoAsync(cancellationToken).ConfigureAwait(false);
        await ResolveDeliveryInfoAsync(cancellationToken).ConfigureAwait(false);
        await ResolvePosPromptVariablesAsync(cancellationToken).ConfigureAwait(false);
        await ResolveItemDescriptionAsync(cancellationToken).ConfigureAwait(false);

        Log.Information("[AiAssistant] Prompt resolved, Prompt: {Prompt}", _ctx.Prompt);
    }

    private async Task LoadAssistantInfoAsync(CancellationToken cancellationToken)
    {
        var (assistant, knowledge, userProfile) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync(_ctx.From, _ctx.To, _ctx.ForwardAssistantId ?? _ctx.AssistantId, cancellationToken).ConfigureAwait(false);

        Log.Information("[AiAssistant] Assistant matched, AssistantId: {AssistantId}, HasProfile: {HasProfile}, From: {From}, To: {To}", assistant?.Id, userProfile != null, _ctx.From, _ctx.To);

        EnsureAssistantInfoComplete(assistant, knowledge);

        _ctx.Assistant = _mapper.Map<AiSpeechAssistantDto>(assistant);
        _ctx.Knowledge = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge);
        _ctx.Prompt = string.Join("\n\n", new[]
            {
                _ctx.Knowledge.Prompt?.Trim(),
                _ctx.Knowledge.ScenePrompt?.Trim()
            }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        _ctx.UserProfileJson = userProfile?.ProfileJson;
    }

    /// <summary>
    /// Validates that the data provider returned both an assistant and an active knowledge entry.
    /// Throws <see cref="AiAssistantNotAvailableException"/> with an actionable message — the
    /// existing <c>ConnectAsync</c> try/catch catches this exception type cleanly and closes
    /// the Twilio WebSocket; raw <c>NullReferenceException</c> would otherwise escape and leave
    /// the call hanging.
    /// Public static; not intended for external use.
    /// </summary>
    public static void EnsureAssistantInfoComplete(AiSpeechAssistantEntity assistant, AiSpeechAssistantKnowledgeEntity knowledge)
    {
        if (assistant == null)
            throw new AiAssistantNotAvailableException("No assistant configured for caller/DID");

        if (knowledge == null)
            throw new AiAssistantNotAvailableException($"No active knowledge for assistant {assistant.Id}");
    }

    private void ResolveStaticPromptVariables()
    {
        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, PstTimeZone.Get());

        _ctx.Prompt = ResolveStaticTokens(_ctx.Prompt, _ctx.UserProfileJson, _ctx.From, pstTime);
    }

    /// <summary>
    /// Pure version of static token replacement, isolated for unit testability.
    /// Returns the prompt unchanged if it is null or empty (no tokens to replace),
    /// matching the safe-no-op contract callers expect.
    /// Public static; not intended for external use.
    /// </summary>
    public static string ResolveStaticTokens(string prompt, string userProfileJson, string from, DateTimeOffset pstTime)
    {
        if (string.IsNullOrEmpty(prompt)) return prompt;

        return prompt
            .Replace("#{user_profile}", string.IsNullOrEmpty(userProfileJson) ? " " : userProfileJson)
            .Replace("#{current_time}", pstTime.ToString("yyyy-MM-dd HH:mm:ss"))
            .Replace("#{customer_phone}", FormatCustomerPhone(from))
            .Replace("#{pst_date}", $"{pstTime.Date:yyyy-MM-dd} {pstTime.DayOfWeek}");
    }

    /// <summary>
    /// Formats the caller phone number for inclusion in the prompt:
    /// strips the leading <c>+1</c> for North American numbers, returns the rest verbatim,
    /// and returns empty string for null/empty input (anonymous Twilio caller edge case).
    /// Public static; not intended for external use.
    /// </summary>
    public static string FormatCustomerPhone(string from)
    {
        if (string.IsNullOrEmpty(from)) return string.Empty;

        // NOTE: matching the original behaviour exactly — `string.StartsWith(string)` uses
        // CurrentCulture comparison. For ASCII "+1" the result is identical to Ordinal,
        // but we preserve the original to avoid any culture-dependent surprise.
        return from.StartsWith("+1") ? from[2..] : from;
    }

    // ── Greeting ──────────────────────────────────────────────────────────
    //
    // Each ResolveX preserves the original check-fetch-apply order so the
    // cross-token cascade (see ShouldResolveCrossTokenCascade_* in the
    // KnowledgeBase fixture) continues to work: any token literal an earlier
    // Resolve injects into _ctx.Prompt is processed by a later Resolve.
    // Internal split into FetchX + ApplyX is a pure structural refactor —
    // no externally observable change, byte-equivalent prompt output.

    private sealed record GreetingFetchResult(string FetchedGreeting);

    private async Task ResolveGreetingAsync(CancellationToken cancellationToken)
    {
        var fetched = await FetchGreetingAsync(cancellationToken).ConfigureAwait(false);
        ApplyGreeting(fetched);
    }

    private async Task<GreetingFetchResult> FetchGreetingAsync(CancellationToken cancellationToken)
    {
        if (!_ctx.NumberId.HasValue || !_ctx.Prompt.Contains("#{greeting}")) return null;

        var response = await _smartiesClient
            .GetSaleAutoCallNumberAsync(new GetSaleAutoCallNumberRequest { Id = _ctx.NumberId.Value }, cancellationToken).ConfigureAwait(false);

        return new GreetingFetchResult(response.Data.Number.Greeting);
    }

    private void ApplyGreeting(GreetingFetchResult result)
    {
        if (result == null) return;

        if (!string.IsNullOrEmpty(result.FetchedGreeting))
            _ctx.Knowledge.Greetings = result.FetchedGreeting;

        _ctx.Prompt = _ctx.Prompt.Replace("#{greeting}", _ctx.Knowledge.Greetings ?? string.Empty);
    }

    // ── Customer items (HiFood / customer_items) ──────────────────────────

    private async Task ResolveCustomerItemsAsync(CancellationToken cancellationToken)
    {
        var fetched = await FetchCustomerItemsAsync(cancellationToken).ConfigureAwait(false);
        ApplyCustomerItems(fetched);
    }

    private async Task<string> FetchCustomerItemsAsync(CancellationToken cancellationToken)
    {
        var hasCustomerItemsToken = _ctx.Prompt.Contains("#{customer_items}", StringComparison.OrdinalIgnoreCase);
        var hasHiFoodItemsToken = _ctx.Prompt.Contains("{HiFood_商品_商品数据}", StringComparison.OrdinalIgnoreCase);

        if (!hasCustomerItemsToken && !hasHiFoodItemsToken) return null;

        if (string.IsNullOrWhiteSpace(_ctx.Assistant.Name)) return null;

        var soldToIds = GetAssistantSoldToIds();
        if (soldToIds.Count == 0) return null;

        var caches = await _salesDataProvider.GetCustomerItemsCacheBySoldToIdsAsync(soldToIds, cancellationToken).ConfigureAwait(false);
        var customerItems = caches.Where(c => !string.IsNullOrEmpty(c.CacheValue)).Select(c => c.CacheValue.Trim()).Distinct().ToList();

        return customerItems.Count > 0
            ? string.Join(Environment.NewLine + Environment.NewLine, customerItems.Take(50))
            : " ";
    }

    private void ApplyCustomerItems(string value)
    {
        if (value == null) return;

        _ctx.Prompt = _ctx.Prompt.Replace("#{customer_items}", value).Replace("{HiFood_商品_商品数据}", value);
    }

    private async Task ResolveDeliveryProgressAsync(CancellationToken cancellationToken)
    {
        if (!_ctx.Prompt.Contains("#{delivery_progress}", StringComparison.OrdinalIgnoreCase)) return;

        var soldToIds = GetAssistantSoldToIds();
        var deliveryProgressText = " ";

        if (soldToIds.Count > 0)
        {
            var caches = await _salesDataProvider.GetDeliveryProgressCacheBySoldToIdsAsync(soldToIds, cancellationToken).ConfigureAwait(false);

            var deliveryProgressValues = soldToIds
                .Select(id => caches.FirstOrDefault(c => string.Equals(c.Filter, id, StringComparison.OrdinalIgnoreCase))?.CacheValue?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (deliveryProgressValues.Count > 0)
                deliveryProgressText = string.Join(Environment.NewLine + Environment.NewLine, deliveryProgressValues);
        }

        _ctx.Prompt = _ctx.Prompt.Replace("#{delivery_progress}", deliveryProgressText);
    }

    // ── Menu items ────────────────────────────────────────────────────────

    private sealed record MenuItemsFetchResult(string MenuItems);

    private async Task ResolveMenuItemsAsync(CancellationToken cancellationToken)
    {
        var fetched = await FetchMenuItemsAsync(cancellationToken).ConfigureAwait(false);
        ApplyMenuItems(fetched);
    }

    private async Task<MenuItemsFetchResult> FetchMenuItemsAsync(CancellationToken cancellationToken)
    {
        if (!_ctx.Prompt.Contains("#{menu_items}", StringComparison.OrdinalIgnoreCase)) return null;

        var menuItems = await GenerateMenuItemsAsync(cancellationToken).ConfigureAwait(false);

        // Wrap so a null/empty fetch result is still distinguishable from "fetch was skipped".
        return new MenuItemsFetchResult(menuItems);
    }

    private void ApplyMenuItems(MenuItemsFetchResult result)
    {
        if (result == null) return;

        _ctx.Prompt = _ctx.Prompt.Replace("#{menu_items}", result.MenuItems ?? "");
    }

    // ── Customer info (CRM / customer_info) ───────────────────────────────

    private async Task ResolveCustomerInfoAsync(CancellationToken cancellationToken)
    {
        var fetched = await FetchCustomerInfoAsync(cancellationToken).ConfigureAwait(false);
        ApplyCustomerInfo(fetched);
    }

    private async Task<string> FetchCustomerInfoAsync(CancellationToken cancellationToken)
    {
        var hasCustomerInfoToken = _ctx.Prompt.Contains("#{customer_info}", StringComparison.OrdinalIgnoreCase);
        var hasCrmCustomerToken = _ctx.Prompt.Contains("{CRM_客户_客户数据}", StringComparison.OrdinalIgnoreCase);

        if (!hasCustomerInfoToken && !hasCrmCustomerToken) return null;

        var cache = await _salesDataProvider.GetCustomerInfoCacheByPhoneNumberAsync(_ctx.From, cancellationToken).ConfigureAwait(false);

        return cache?.CacheValue?.Trim() ?? " ";
    }

    private void ApplyCustomerInfo(string value)
    {
        if (value == null) return;

        _ctx.Prompt = _ctx.Prompt
            .Replace("#{customer_info}", value)
            .Replace("{CRM_客户_客户数据}", value);
    }

    // ── POS prompt variables (products + store hours) ─────────────────────

    private sealed record PosPromptFetchResult(
        bool NeedProducts,
        bool NeedStoreHours,
        List<string> RequestedProductTokens,
        TranscriptionLanguage Language,
        List<PosMenuProductBriefDto> Products,
        string StoreHours);

    private async Task ResolvePosPromptVariablesAsync(CancellationToken cancellationToken)
    {
        var fetched = await FetchPosPromptVariablesAsync(cancellationToken).ConfigureAwait(false);
        ApplyPosPromptVariables(fetched);
    }

    private async Task<PosPromptFetchResult> FetchPosPromptVariablesAsync(CancellationToken cancellationToken)
    {
        var requestedProductTokens = GetRequestedPosProductTokens();
        var needProducts = requestedProductTokens.Count > 0;
        var needStoreHours = HasPromptToken("{POS_店铺信息_营业时间}");

        if (!needProducts && !needStoreHours) return null;

        var language = ResolvePosPromptLanguage();
        var products = needProducts
            ? await _posUtilService.GetPosMenuProductBriefsAsync(_ctx.AgentId, language, cancellationToken).ConfigureAwait(false)
            : new List<PosMenuProductBriefDto>();

        var storeHours = needStoreHours
            ? await _posUtilService.GetPosStoreTimePeriodsAsync(_ctx.AgentId, language, cancellationToken).ConfigureAwait(false)
            : null;

        return new PosPromptFetchResult(needProducts, needStoreHours, requestedProductTokens, language, products, storeHours);
    }

    private void ApplyPosPromptVariables(PosPromptFetchResult result)
    {
        if (result == null) return;

        if (result.NeedProducts)
        {
            if (result.RequestedProductTokens.Count > 2)
            {
                ResolveCompactPosProductData(result.Products, result.RequestedProductTokens, result.Language);
            }
            else
            {
                ResolvePosMenuProductNames(result.Products);
                ResolvePosMenuProductCategories(result.Products);
                ResolvePosMenuProductSpecifications(result.Products);
                ResolvePosMenuProductTaxes(result.Products);
                ResolvePosMenuProductPrices(result.Products);
                ResolvePosMenuProductTimes(result.Products);
            }
        }

        if (result.NeedStoreHours)
            ResolvePosStoreBusinessHours(result.StoreHours);
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

    private void ResolveCompactPosProductData(List<PosMenuProductBriefDto> products, List<string> requestedTokens, TranscriptionLanguage language)
    {
        var compactValue = BuildCompactPosProductData(products, requestedTokens, language);

        if (string.IsNullOrWhiteSpace(compactValue))
        {
            foreach (var token in requestedTokens)
                ReplacePromptToken(token, " ");

            return;
        }

        ReplacePromptToken(requestedTokens[0], compactValue);

        foreach (var token in requestedTokens.Skip(1))
            ReplacePromptToken(token, "见上方POS压缩数据");
    }

    private string BuildCompactPosProductData(List<PosMenuProductBriefDto> products, List<string> requestedTokens, TranscriptionLanguage language)
    {
        var entries = BuildCompactPosProductEntries(products, language);
        if (entries.Count == 0) return " ";

        var categoryCodebook = requestedTokens.Contains(PosMenuProductCategoryToken)
            ? BuildCompactCodebook(entries.SelectMany(entry => entry.Categories), "C")
            : null;
        var menuTimeCodebook = requestedTokens.Contains(PosMenuProductTimeToken)
            ? BuildCompactCodebook(entries.SelectMany(entry => entry.MenuTimes), "R")
            : null;
        var taxCodebook = requestedTokens.Contains(PosMenuProductTaxToken)
            ? BuildCompactCodebook(entries.SelectMany(entry => entry.Taxes), "T")
            : null;
        var specificationCodebook = requestedTokens.Contains(PosMenuProductSpecificationToken)
            ? BuildCompactCodebook(entries.SelectMany(entry => entry.Specifications), "S")
            : null;

        var sections = new List<string>
        {
            "POS压缩数据(先按码表解码;回答时用商品名称,不要直接回复P/C/R/T/S编码)",
            BuildCompactPosProductHeader(requestedTokens),
            string.Join(Environment.NewLine, entries.Select(entry => BuildCompactPosProductRow(entry, requestedTokens, categoryCodebook, menuTimeCodebook, taxCodebook, specificationCodebook)))
        };

        AppendCompactProductCodebookSection(sections, entries);
        AppendCompactCodebookSection(sections, "C", categoryCodebook);
        AppendCompactCodebookSection(sections, "R", menuTimeCodebook);
        AppendCompactCodebookSection(sections, "T", taxCodebook);
        AppendCompactCodebookSection(sections, "S", specificationCodebook);

        return string.Join(Environment.NewLine, sections);
    }

    private static string BuildCompactPosProductHeader(List<string> requestedTokens)
    {
        var columns = new List<string> { "P" };

        if (requestedTokens.Contains(PosMenuProductCategoryToken))
            columns.Add("C");

        if (requestedTokens.Contains(PosMenuProductTimeToken))
            columns.Add("R");

        if (requestedTokens.Contains(PosMenuProductPriceToken))
            columns.Add("价");

        if (requestedTokens.Contains(PosMenuProductTaxToken))
            columns.Add("T");

        if (requestedTokens.Contains(PosMenuProductSpecificationToken))
            columns.Add("S");

        return string.Join("|", columns);
    }

    private static string BuildCompactPosProductRow(
        PosCompactProductEntry entry,
        List<string> requestedTokens,
        PosCompactCodebook categoryCodebook,
        PosCompactCodebook menuTimeCodebook,
        PosCompactCodebook taxCodebook,
        PosCompactCodebook specificationCodebook)
    {
        var columns = new List<string>
        {
            entry.ProductCode
        };

        if (requestedTokens.Contains(PosMenuProductCategoryToken))
            columns.Add(ResolveCompactCodes(entry.Categories, categoryCodebook));

        if (requestedTokens.Contains(PosMenuProductTimeToken))
            columns.Add(ResolveCompactCodes(entry.MenuTimes, menuTimeCodebook));

        if (requestedTokens.Contains(PosMenuProductPriceToken))
            columns.Add(entry.Price);

        if (requestedTokens.Contains(PosMenuProductTaxToken))
            columns.Add(ResolveCompactCodes(entry.Taxes, taxCodebook));

        if (requestedTokens.Contains(PosMenuProductSpecificationToken))
            columns.Add(ResolveCompactCodes(entry.Specifications, specificationCodebook));

        return string.Join("|", columns);
    }

    private List<PosCompactProductEntry> BuildCompactPosProductEntries(List<PosMenuProductBriefDto> products, TranscriptionLanguage language)
    {
        return products
            .Select((product, index) => new PosCompactProductEntry(
                $"P{index + 1:000}",
                BuildCompactProductName(product, language).Trim(),
                SplitCompactValues(product?.CategoryName, "、"),
                (product?.PosMenus ?? [])
                    .Select(BuildPosMenuPeriod)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                product?.Price.ToString("0.##") ?? "0",
                SplitCompactTaxValues(product?.Tax),
                SplitCompactValues(product?.Specification, "；")))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ProductName))
            .ToList();
    }

    private static PosCompactCodebook BuildCompactCodebook(IEnumerable<string> values, string prefix)
    {
        var items = new List<PosCompactCodeItem>();
        var valueToCode = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var value in values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
        {
            var code = $"{prefix}{items.Count + 1}";
            items.Add(new PosCompactCodeItem(code, value));
            valueToCode[value] = code;
        }

        return new PosCompactCodebook(items, valueToCode);
    }

    private static void AppendCompactCodebookSection(List<string> sections, string label, PosCompactCodebook codebook)
    {
        if (codebook is null || codebook.Items.Count == 0) return;

        sections.Add($"{label}:");
        sections.Add(string.Join(Environment.NewLine, codebook.Items.Select(item => $"{item.Code}={item.Value}")));
    }

    private static void AppendCompactProductCodebookSection(List<string> sections, List<PosCompactProductEntry> entries)
    {
        if (entries.Count == 0) return;

        sections.Add("P:");
        sections.Add(string.Join(Environment.NewLine, entries.Select(entry => $"{entry.ProductCode}={entry.ProductName}")));
    }

    private static string ResolveCompactCodes(IEnumerable<string> values, PosCompactCodebook codebook)
    {
        var codes = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(value => codebook != null && codebook.ValueToCode.TryGetValue(value, out var code) ? code : value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return codes.Count == 0 ? "-" : string.Join("+", codes);
    }

    private static List<string> SplitCompactValues(string value, string separator)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        return value
            .Split([separator], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> SplitCompactTaxValues(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        return value
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeCompactTaxValue)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string NormalizeCompactTaxValue(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;

        return normalized.Contains('%', StringComparison.Ordinal) || normalized.StartsWith('$')
            ? normalized
            : $"${normalized}";
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

    private static string BuildCompactProductName(PosMenuProductBriefDto product, TranscriptionLanguage language)
    {
        var cn = product?.NameCn?.Trim() ?? string.Empty;
        var en = product?.NameEn?.Trim() ?? string.Empty;
        var fallback = product?.Name?.Trim() ?? string.Empty;

        return language switch
        {
            TranscriptionLanguage.Chinese when !string.IsNullOrWhiteSpace(cn) => cn,
            TranscriptionLanguage.English when !string.IsNullOrWhiteSpace(en) => en,
            TranscriptionLanguage.Spanish when !string.IsNullOrWhiteSpace(en) => en,
            TranscriptionLanguage.Korean when !string.IsNullOrWhiteSpace(en) => en,
            _ when !string.IsNullOrWhiteSpace(fallback) => fallback,
            _ when !string.IsNullOrWhiteSpace(cn) => cn,
            _ => en
        };
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

    // ── Delivery info (CRM 送货日 / delivery_info) ─────────────────────────

    private sealed record DeliveryInfoFetchResult(string DeliveryValue);

    private async Task ResolveDeliveryInfoAsync(CancellationToken cancellationToken)
    {
        var fetched = await FetchDeliveryInfoAsync(cancellationToken).ConfigureAwait(false);
        ApplyDeliveryInfo(fetched);
    }

    private async Task<DeliveryInfoFetchResult> FetchDeliveryInfoAsync(CancellationToken cancellationToken)
    {
        if (!HasDeliveryInfoToken(_ctx.Prompt)) return null;

        var cache = await _salesDataProvider.GetDeliveryInfoCacheByPhoneNumberAsync(_ctx.From, cancellationToken).ConfigureAwait(false);

        return new DeliveryInfoFetchResult(cache?.CacheValue?.Trim());
    }

    private void ApplyDeliveryInfo(DeliveryInfoFetchResult result)
    {
        if (result == null) return;

        _ctx.Prompt = ApplyDeliveryInfoTokens(_ctx.Prompt, result.DeliveryValue);
    }

    private List<string> GetAssistantSoldToIds()
    {
        if (string.IsNullOrWhiteSpace(_ctx.Assistant?.Name))
            return [];

        var soldToIds = new List<string>();

        foreach (var soldToId in _ctx.Assistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmedSoldToId = soldToId.Trim();
            if (string.IsNullOrWhiteSpace(trimmedSoldToId)) continue;
            if (soldToIds.Contains(trimmedSoldToId, StringComparer.OrdinalIgnoreCase)) continue;

            soldToIds.Add(trimmedSoldToId);
        }

        return soldToIds;
    }

    /// <summary>
    /// Returns true if the prompt contains either delivery info token literal.
    /// Supports canonical Chinese token <c>#{CRM_路线_送货日数据}</c> and legacy <c>{CRM_路线_送货日数据}</c>.
    /// Public static for unit testability; not intended for external use.
    /// </summary>
    public static bool HasDeliveryInfoToken(string prompt) =>
        !string.IsNullOrEmpty(prompt) &&
        (prompt.Contains("#{delivery_info}", StringComparison.OrdinalIgnoreCase) ||
         prompt.Contains("#{CRM_路线_送货日数据}", StringComparison.OrdinalIgnoreCase) ||
         prompt.Contains("{CRM_路线_送货日数据}", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Replaces both delivery info tokens with the given value.
    /// Null/empty <paramref name="deliveryValue"/> becomes a single space placeholder
    /// (preserves V2's existing <c>?? " "</c> semantic so the model never sees a literal token).
    /// Public static for unit testability; not intended for external use.
    /// </summary>
    public static string ApplyDeliveryInfoTokens(string prompt, string deliveryValue)
    {
        if (string.IsNullOrEmpty(prompt)) return prompt;

        var value = string.IsNullOrEmpty(deliveryValue) ? " " : deliveryValue;

        return prompt
            .Replace("#{delivery_info}", value)
            .Replace("#{CRM_路线_送货日数据}", value)
            .Replace("{CRM_路线_送货日数据}", value);
    }

    private async Task ResolveItemDescriptionAsync(CancellationToken cancellationToken)
    {
        if (!_ctx.Prompt.Contains("{HiFood_商品_术语库需求}", StringComparison.OrdinalIgnoreCase)) return;

        var descriptions = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantDescriptionsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var descriptionList = string.Join(
            "\n",
            descriptions.Select(x =>
                string.IsNullOrWhiteSpace(x.ModelDescription)
                    ? x.ModelValue
                    : $"{x.ModelValue}（{x.ModelDescription}）"));

        _ctx.Prompt = _ctx.Prompt.Replace("#{HiFood_商品_术语库需求}", descriptionList);
    }

    private sealed record PosCompactProductEntry(
        string ProductCode,
        string ProductName,
        List<string> Categories,
        List<string> MenuTimes,
        string Price,
        List<string> Taxes,
        List<string> Specifications);

    private sealed record PosCompactCodeItem(
        string Code,
        string Value);

    private sealed record PosCompactCodebook(
        List<PosCompactCodeItem> Items,
        Dictionary<string, string> ValueToCode);
}
