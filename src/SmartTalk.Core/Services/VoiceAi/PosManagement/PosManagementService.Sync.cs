using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;
using SmartTalk.Messages.Dto.EasyPos;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementService : IScopedDependency
{
    Task<SyncPosConfigurationResponse> SyncPosConfigurationAsync(SyncPosConfigurationCommand command, CancellationToken cancellationToken);
}

public partial class PosManagementService 
{
    public async Task<SyncPosConfigurationResponse> SyncPosConfigurationAsync(SyncPosConfigurationCommand command, CancellationToken cancellationToken)
    {
        var store = await _posManagementDataProvider.GetPosCompanyStoreAsync(command.StoreId, cancellationToken).ConfigureAwait(false);

        if (store == null) Log.Information("获取到门店信息: {@store}", store);

        var posConfiguration = await _easyPosClient.GetEasyPosRestaurantMenusAsync(store.EnName, cancellationToken).ConfigureAwait(false);

        Log.Information("获取到 POS 配置数据: {@posConfiguration}", posConfiguration);
        
        var storeOpeningHours = posConfiguration.Data.TimePeriods; 
        await UpdateStoreBusinessTimePeriodsAsync(store, storeOpeningHours, cancellationToken);
        
        await SyncMenuDataAsync(store, posConfiguration.Data, cancellationToken);
        
        return new SyncPosConfigurationResponse
        {
            Data = posConfiguration
        };
    }
    
    private async Task UpdateStoreBusinessTimePeriodsAsync(PosCompanyStore store, List<EasyPosResponseTimePeriod> timePeriods, CancellationToken cancellationToken)
    {
        store.TimePeriod = JsonConvert.SerializeObject(timePeriods);
        await _posManagementDataProvider.UpdateStoreAsync(store, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncMenuDataAsync(PosCompanyStore store, EasyPosResponseData data, CancellationToken cancellationToken)
    {
        var menus = GenerateMenusFromResponse(store, data.Menus);
        await _posManagementDataProvider.UpdateStoreMenusAsync(menus, true, cancellationToken);

        foreach (var menu in data.Menus)
        {
            var categories = GenerateCategoriesForMenu(menu, data.Categories);
            await _posManagementDataProvider.UpdateStoreCategoriesAsync(categories, true, cancellationToken);

            foreach (var category in categories)
            {
                var products = GenerateProductsForCategory(category, data.Products);
                await _posManagementDataProvider.UpdateStoreProductsAsync(products, true, cancellationToken);
            }
        }
    }
    
    private List<PosMenu> GenerateMenusFromResponse(PosCompanyStore store, List<EasyPosResponseMenu> menus)
    {
        return menus.Select(menu => new PosMenu
        {
            StoreId = store.Id,
            MenuId = menu.Id.ToString(),
            Names = JsonConvert.SerializeObject(GetLocalizedNames(menu.Localizations)),
            TimePeriod = JsonConvert.SerializeObject(menu.TimePeriods),
            CategoryIds = menu.CategoryIds == null ? string.Empty : string.Join(",", menu.CategoryIds),
            Status = menu.Status,
            CreatedBy = _currentUser.Id
        }).ToList();
    }
    
    private List<PosCategory> GenerateCategoriesForMenu(EasyPosResponseMenu menu, List<EasyPosResponseCategory> allCategories)
    {
        return allCategories
            .Where(c => c.MenuIds.Contains(menu.Id.ToString()))
            .Select(category => new PosCategory
            {
                MenuId = menu.Id.ToString(),
                CategoryId = category.Id.ToString(),
                Names = JsonConvert.SerializeObject(GetLocalizedNames(category.Localizations)),
                MenuIds = string.Join(",", category.MenuIds)
            }).ToList();
    }
    
    private List<PosProduct> GenerateProductsForCategory(PosCategory category, List<EasyPosResponseProduct> allProducts)
    {
        return allProducts
            .Where(p => p.CategoryId.ToString() == category.CategoryId)
            .Select(product => new PosProduct
            {
                ProductId = product.Id.ToString(),
                CategoryId = product.CategoryId.ToString(),
                Price = product.Price,
                Status = true,
                Names = JsonConvert.SerializeObject(GetLocalizedNames(product.Localizations)),
                Modifiers = product.ModifierGroups != null ? JsonConvert.SerializeObject(product.ModifierGroups) : null,
                Tax = product.Taxes != null ? JsonConvert.SerializeObject(product.Taxes) : null
            }).ToList();
    }
    
    private Dictionary<string, Dictionary<string, string>> GetLocalizedNames(IEnumerable<EasyPosResponseLocalization> localizations)
    {
        var languageCodeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh_CN"] = "cn",
            ["en_US"] = "en"
        };

        return localizations
            .Where(loc => !string.IsNullOrWhiteSpace(loc.LanguageCode) && !string.IsNullOrWhiteSpace(loc.Field))
            .GroupBy(loc => loc.LanguageCode)
            .Where(g => languageCodeMap.ContainsKey(g.Key))
            .ToDictionary(
                g => languageCodeMap[g.Key],
                g => g.ToDictionary(x => x.Field, x => x.Value)
            );
    }
}