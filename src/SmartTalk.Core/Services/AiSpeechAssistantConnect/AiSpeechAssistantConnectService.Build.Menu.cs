using Newtonsoft.Json;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task<string> GenerateMenuItemsAsync(CancellationToken cancellationToken)
    {
        var storeAgent = await _posDataProvider.GetPosAgentByAgentIdAsync(_ctx.AgentId, cancellationToken).ConfigureAwait(false);
        
        if (storeAgent == null) return null;

        var (products, categories) = await LoadStoreMenuDataAsync(storeAgent.StoreId, cancellationToken).ConfigureAwait(false);
        
        var selected = SelectMenuProducts(products);
        var grouped = GroupProductsByCategory(selected, categories);

        return FormatMenu(grouped);
    }

    private async Task<(List<PosProduct> Products, List<PosCategory> Categories)> LoadStoreMenuDataAsync(int storeId, CancellationToken cancellationToken)
    {
        var products = await _posDataProvider.GetPosProductsByAgentIdAsync(_ctx.AgentId, cancellationToken).ConfigureAwait(false);
        var categories = (await _posDataProvider.GetPosCategoriesAsync(storeId: storeId, cancellationToken: cancellationToken).ConfigureAwait(false)).DistinctBy(x => x.CategoryId).ToList();

        return (products, categories);
    }

    private static List<PosProduct> SelectMenuProducts(List<PosProduct> products)
    {
        var ordered = products.OrderBy(x => x.SortOrder).ToList();

        var normal = ordered.Where(x => !HasModifiers(x)).Take(80);
        var withModifiers = ordered.Where(HasModifiers).Take(20);

        return normal.Concat(withModifiers).ToList();
    }

    private static bool HasModifiers(PosProduct product) => !string.IsNullOrEmpty(product.Modifiers) && product.Modifiers != "[]";

    private static Dictionary<PosCategory, List<PosProduct>> GroupProductsByCategory(List<PosProduct> products, List<PosCategory> categories)
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

    private static string FormatMenu(Dictionary<PosCategory, List<PosProduct>> grouped) =>
        string.Join("\n", grouped
            .Where(g => g.Value.Count > 0)
            .Select(g => FormatCategory(g.Key, g.Value))
            .Where(s => !string.IsNullOrEmpty(s))).TrimEnd('\r', '\n');

    private static string FormatCategory(PosCategory category, List<PosProduct> products)
    {
        var categoryName = BuildMenuItemName(JsonConvert.DeserializeObject<PosNamesLocalization>(category.Names));
        
        if (string.IsNullOrWhiteSpace(categoryName)) return null;

        var lines = products
            .Select(FormatProduct)
            .Where(l => l != null)
            .Select((l, i) => $"{i + 1}. {l}");

        return categoryName + "\n" + string.Join("\n", lines);
    }

    private static string FormatProduct(PosProduct product)
    {
        var name = BuildMenuItemName(JsonConvert.DeserializeObject<PosNamesLocalization>(product.Names));
        
        if (string.IsNullOrWhiteSpace(name)) return null;

        return $"{name}：${product.Price:F2}{FormatModifiers(product.Modifiers)}";
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
