using Newtonsoft.Json;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task<string> GenerateMenuItemsAsync(CancellationToken cancellationToken)
    {
        var storeAgent = (await _posDataProvider.GetPosAgentByAgentIdsAsync([_ctx.AgentId], cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (storeAgent == null) return null;

        var storeProducts = await _posDataProvider.GetPosProductsByAgentIdAsync(_ctx.AgentId, cancellationToken).ConfigureAwait(false);
        var storeCategories = (await _posDataProvider.GetPosCategoriesAsync(storeId: storeAgent.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false)).DistinctBy(x => x.CategoryId).ToList();

        var normalProducts = storeProducts.OrderBy(x => x.SortOrder).Where(x => x.Modifiers == "[]").Take(80).ToList();
        var modifierProducts = storeProducts.OrderBy(x => x.SortOrder).Where(x => x.Modifiers != "[]").Take(20).ToList();

        var grouped = GroupProductsByCategory(normalProducts.Concat(modifierProducts).ToList(), storeCategories);

        return FormatMenu(grouped);
    }

    private static Dictionary<PosCategory, List<PosProduct>> GroupProductsByCategory(
        List<PosProduct> products, List<PosCategory> categories)
    {
        var lookup = new Dictionary<PosCategory, List<PosProduct>>();

        foreach (var product in products)
        {
            var category = categories.FirstOrDefault(c => c.Id == product.CategoryId);
            if (category == null) continue;

            if (!lookup.ContainsKey(category))
                lookup[category] = new List<PosProduct>();

            lookup[category].Add(product);
        }

        return lookup;
    }

    private static string FormatMenu(Dictionary<PosCategory, List<PosProduct>> grouped)
    {
        var menuItems = string.Empty;

        foreach (var (category, products) in grouped)
        {
            if (products.Count == 0) continue;

            var categoryName = BuildMenuItemName(JsonConvert.DeserializeObject<PosNamesLocalization>(category.Names));
            if (string.IsNullOrWhiteSpace(categoryName)) continue;

            var productDetails = categoryName + "\n";
            var idx = 1;

            foreach (var product in products)
            {
                var productName = BuildMenuItemName(JsonConvert.DeserializeObject<PosNamesLocalization>(product.Names));
                if (string.IsNullOrWhiteSpace(productName)) continue;

                productDetails += $"{idx}. {productName}：${product.Price:F2}{FormatModifiers(product.Modifiers)}\n";
                idx++;
            }

            menuItems += productDetails + "\n";
        }

        return menuItems.TrimEnd('\r', '\n');
    }

    private static string FormatModifiers(string modifiersJson)
    {
        if (string.IsNullOrEmpty(modifiersJson)) return string.Empty;

        var modifiers = JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(modifiersJson);
        if (modifiers is not { Count: > 0 }) return string.Empty;

        return string.Concat(modifiers.Select(FormatSingleModifier));
    }

    private static string FormatSingleModifier(EasyPosResponseModifierGroups modifier)
    {
        var names = modifier.ModifierProducts?
            .Select(mp => BuildModifierName(mp.Localizations))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList() ?? [];

        if (names.Count == 0) return string.Empty;

        return $" {BuildModifierName(modifier.Localizations)}規格：{string.Join("、", names)}，共{names.Count}个规格，要求最少选{modifier.MinimumSelect}个规格，最多选{modifier.MaximumSelect}规格，每个最大可重复选{modifier.MaximumRepetition}相同的 \n";
    }

    private static string ResolveLocalizedName(params string[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? string.Empty;

    private static string BuildMenuItemName(PosNamesLocalization l) =>
        ResolveLocalizedName(
            l?.Cn?.Name, l?.En?.Name,
            l?.Cn?.PosName, l?.En?.PosName,
            l?.Cn?.SendChefName, l?.En?.SendChefName);

    private static string BuildModifierName(List<EasyPosResponseLocalization> localizations) =>
        ResolveLocalizedName(
            localizations.Find(x => x.LanguageCode == "zh_CN" && x.Field == "name")?.Value,
            localizations.Find(x => x.LanguageCode == "en_US" && x.Field == "name")?.Value,
            localizations.Find(x => x.LanguageCode == "zh_CN" && x.Field == "posName")?.Value,
            localizations.Find(x => x.LanguageCode == "en_US" && x.Field == "posName")?.Value,
            localizations.Find(x => x.LanguageCode == "zh_CN" && x.Field == "sendChefName")?.Value,
            localizations.Find(x => x.LanguageCode == "en_US" && x.Field == "sendChefName")?.Value);
}
