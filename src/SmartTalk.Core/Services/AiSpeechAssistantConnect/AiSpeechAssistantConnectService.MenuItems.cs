using Newtonsoft.Json;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task<string> GenerateMenuItemsAsync(int agentId, CancellationToken cancellationToken = default)
    {
        var storeAgent = await _posDataProvider.GetPosAgentByAgentIdAsync(agentId, cancellationToken).ConfigureAwait(false);

        if (storeAgent == null) return null;

        var storeProducts = await _posDataProvider.GetPosProductsByAgentIdAsync(agentId, cancellationToken).ConfigureAwait(false);
        var storeCategories = (await _posDataProvider.GetPosCategoriesAsync(storeId: storeAgent.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false)).DistinctBy(x => x.CategoryId).ToList();

        var normalProducts = storeProducts.OrderBy(x => x.SortOrder).Where(x => x.Modifiers == "[]").Take(80).ToList();
        var modifierProducts = storeProducts.OrderBy(x => x.SortOrder).Where(x => x.Modifiers != "[]").Take(20).ToList();

        var partialProducts = normalProducts.Concat(modifierProducts).ToList();
        var categoryProductsLookup = new Dictionary<PosCategory, List<PosProduct>>();

        foreach (var product in partialProducts)
        {
            var category = storeCategories.FirstOrDefault(c => c.Id == product.CategoryId);
            if (category == null) continue;

            if (!categoryProductsLookup.ContainsKey(category))
                categoryProductsLookup[category] = new List<PosProduct>();

            categoryProductsLookup[category].Add(product);
        }

        var menuItems = string.Empty;

        foreach (var (category, products) in categoryProductsLookup)
        {
            if (products.Count == 0) continue;

            var productDetails = string.Empty;
            var categoryNames = JsonConvert.DeserializeObject<PosNamesLocalization>(category.Names);

            var categoryName = BuildMenuItemName(categoryNames);

            if (string.IsNullOrWhiteSpace(categoryName)) continue;

            var idx = 1;
            productDetails += categoryName + "\n";

            foreach (var product in products)
            {
                var productNames = JsonConvert.DeserializeObject<PosNamesLocalization>(product.Names);

                var productName = BuildMenuItemName(productNames);

                if (string.IsNullOrWhiteSpace(productName)) continue;
                var line = $"{idx}. {productName}：${product.Price:F2}";

                if (!string.IsNullOrEmpty(product.Modifiers))
                {
                    var modifiers = JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(product.Modifiers);

                    if (modifiers is { Count: > 0 })
                    {
                        var modifiersDetail = string.Empty;

                        foreach (var modifier in modifiers)
                        {
                            var modifierNames = new List<string>();

                            if (modifier.ModifierProducts != null && modifier.ModifierProducts.Count != 0)
                            {
                                foreach (var mp in modifier.ModifierProducts)
                                {
                                    var name = BuildModifierName(mp.Localizations);

                                    if (!string.IsNullOrWhiteSpace(name)) modifierNames.Add($"{name}");
                                }
                            }

                            if (modifierNames.Count > 0)
                                modifiersDetail += $" {BuildModifierName(modifier.Localizations)}規格：{string.Join("、", modifierNames)}，共{modifierNames.Count}个规格，要求最少选{modifier.MinimumSelect}个规格，最多选{modifier.MaximumSelect}规格，每个最大可重复选{modifier.MaximumRepetition}相同的 \n";
                        }

                        line += modifiersDetail;
                    }
                }

                idx++;
                productDetails += line + "\n";
            }

            menuItems += productDetails + "\n";
        }

        return menuItems.TrimEnd('\r', '\n');
    }

    private static string BuildMenuItemName(PosNamesLocalization localization)
    {
        var zhName = !string.IsNullOrWhiteSpace(localization?.Cn?.Name) ? localization.Cn.Name : string.Empty;
        if (!string.IsNullOrWhiteSpace(zhName)) return zhName;

        var usName = !string.IsNullOrWhiteSpace(localization?.En?.Name) ? localization.En.Name : string.Empty;
        if (!string.IsNullOrWhiteSpace(usName)) return usName;

        var zhPosName = !string.IsNullOrWhiteSpace(localization?.Cn?.PosName) ? localization.Cn.PosName : string.Empty;
        if (!string.IsNullOrWhiteSpace(zhPosName)) return zhPosName;

        var usPosName = !string.IsNullOrWhiteSpace(localization?.En?.PosName) ? localization.En.PosName : string.Empty;
        if (!string.IsNullOrWhiteSpace(usPosName)) return usPosName;

        var zhSendChefName = !string.IsNullOrWhiteSpace(localization?.Cn?.SendChefName) ? localization.Cn.SendChefName : string.Empty;
        if (!string.IsNullOrWhiteSpace(zhSendChefName)) return zhSendChefName;

        var usSendChefName = !string.IsNullOrWhiteSpace(localization?.En?.SendChefName) ? localization.En.SendChefName : string.Empty;
        if (!string.IsNullOrWhiteSpace(usSendChefName)) return usSendChefName;

        return string.Empty;
    }

    private static string BuildModifierName(List<EasyPosResponseLocalization> localizations)
    {
        var zhName = localizations.Find(l => l.LanguageCode == "zh_CN" && l.Field == "name");
        if (zhName != null && !string.IsNullOrWhiteSpace(zhName.Value)) return zhName.Value;

        var usName = localizations.Find(l => l.LanguageCode == "en_US" && l.Field == "name");
        if (usName != null && !string.IsNullOrWhiteSpace(usName.Value)) return usName.Value;

        var zhPosName = localizations.Find(l => l.LanguageCode == "zh_CN" && l.Field == "posName");
        if (zhPosName != null && !string.IsNullOrWhiteSpace(zhPosName.Value)) return zhPosName.Value;

        var usPosName = localizations.Find(l => l.LanguageCode == "en_US" && l.Field == "posName");
        if (usPosName != null && !string.IsNullOrWhiteSpace(usPosName.Value)) return usPosName.Value;

        var zhSendChefName = localizations.Find(l => l.LanguageCode == "zh_CN" && l.Field == "sendChefName");
        if (zhSendChefName != null && !string.IsNullOrWhiteSpace(zhSendChefName.Value)) return zhSendChefName.Value;

        var usSendChefName = localizations.Find(l => l.LanguageCode == "en_US" && l.Field == "sendChefName");
        if (usSendChefName != null && !string.IsNullOrWhiteSpace(usSendChefName.Value)) return usSendChefName.Value;

        return string.Empty;
    }
}
