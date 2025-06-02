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
        
        if (store == null) throw new Exception($"Can't find store with id：，StoreId: {command.StoreId}");
        
        Log.Information("Get the store info: {@store}", store);
        
        var posConfiguration = await _easyPosClient.GetPosCompanyStoreMenusAsync(
            new EasyPosTokenRequestDto { AppId = store.AppId, AppSecret = store.AppSecret }, cancellationToken).ConfigureAwait(false);

        Log.Information("Get the pos configuration: {@posConfiguration}", posConfiguration);
        
        var storeOpeningHours = posConfiguration?.Data?.TimePeriods; 
        await UpdateStoreBusinessTimePeriodsAsync(store, storeOpeningHours, cancellationToken).ConfigureAwait(false);
        
        await SyncMenuDataAsync(store, posConfiguration?.Data, cancellationToken).ConfigureAwait(false);
        
        return new SyncPosConfigurationResponse
        {
            Data = posConfiguration
        };
    }
    
    private async Task UpdateStoreBusinessTimePeriodsAsync(PosCompanyStore store, List<EasyPosResponseTimePeriod> timePeriods, CancellationToken cancellationToken)
    {
        store.TimePeriod = timePeriods != null && timePeriods.Count != 0 ? JsonConvert.SerializeObject(timePeriods.First()) : string.Empty;
        await _posManagementDataProvider.UpdateStoreAsync(store, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncMenuDataAsync(PosCompanyStore store, EasyPosResponseData data, CancellationToken cancellationToken)
    {
        if (data?.Menus == null) throw new NullReferenceException("Pos Resource Data or Menus is null");
        
        await _posManagementDataProvider.DeletePosMenuInfosAsync(store.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

        var menuMap = await AddPosMenusAsync(data.Menus, store.Id, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Sync menu data: {@MenuMap}", menuMap);
        
        var categoriesMap = await AddPosCategoriesAsync(data, menuMap, store.Id, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Sync categories data: {@CategoriesMap}", categoriesMap);
        
        await AddPosProductsAsync(data, categoriesMap, store.Id, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task<Dictionary<string, int>> AddPosMenusAsync(List<EasyPosResponseMenu> menus, int storeId, CancellationToken cancellationToken)
    {
        var posMenus = menus.Select(x => new PosMenu
        {
            StoreId = storeId,
            MenuId = x.Id.ToString(),
            Names = JsonConvert.SerializeObject(GetLocalizedNames(x.Localizations)),
            TimePeriod = JsonConvert.SerializeObject(x.TimePeriods),
            CategoryIds = x.CategoryIds == null ? string.Empty : string.Join(",", x.CategoryIds),
            Status = x.Status,
            CreatedBy = _currentUser.Id
        }).ToList();
        
        await _posManagementDataProvider.AddPosMenusAsync(posMenus, true, cancellationToken).ConfigureAwait(false);
        
        return posMenus.ToDictionary(m => m.MenuId, m => m.Id);
    }
    
    private async Task<Dictionary<long, Dictionary<string, int>>> AddPosCategoriesAsync(EasyPosResponseData data, Dictionary<string, int> menuMap, int storeId, CancellationToken cancellationToken)
    {
        var posCategories = new List<PosCategory>();
        var mapping = new Dictionary<long, List<PosCategory>>();
        
        foreach (var menu in data.Menus)
        {
            if (!menuMap.TryGetValue(menu.Id.ToString(), out var posMenuId))
                continue;

            var categories = menu.Categories.Where(c => c.MenuIds.Contains(menu.Id)).Select(x => new PosCategory
            {
                MenuId = posMenuId,
                StoreId = storeId,
                CategoryId = x.Id.ToString(),
                Names = JsonConvert.SerializeObject(GetLocalizedNames(x.Localizations)),
                MenuIds = string.Join(",", x.MenuIds ?? []),
                MenuNames = JsonConvert.SerializeObject(GetLocalizedNames(menu.Localizations)),
                CreatedBy = _currentUser.Id
            }).ToList();
            
            posCategories.AddRange(categories);
            mapping[menu.Id] = categories;
        }
        
        await _posManagementDataProvider.AddPosCategoriesAsync(posCategories, true, cancellationToken).ConfigureAwait(false);

        return BuildMenuToCategoriesMapping(mapping);
    }

    private Dictionary<long, Dictionary<string, int>> BuildMenuToCategoriesMapping(Dictionary<long, List<PosCategory>> menuToCategoriesMap)
    {
        var mapping = new Dictionary<long, Dictionary<string, int>>();

        foreach (var (menuId, originalCategories) in menuToCategoriesMap)
        {
            mapping[menuId] = originalCategories.ToDictionary(c => c.CategoryId, c => c.Id);
        }
        
        return mapping;
    }
    
    private async Task AddPosProductsAsync(EasyPosResponseData data, Dictionary<long, Dictionary<string, int>> categoriesMap, int storeId, CancellationToken cancellationToken)
    {
        var posProducts = new List<PosProduct>();
        
        foreach (var menu in data.Menus)
        {
            foreach (var category in menu.Categories.Where(c => c.MenuIds.Contains(menu.Id)))
            {
                if (categoriesMap.TryGetValue(menu.Id, out var categoryMap) && categoryMap.TryGetValue(category.Id.ToString(), out var posCategoryId))
                {
                    var products = category.Products.Where(p => p.CategoryIds.Contains(category.Id))
                        .Select(product => new PosProduct
                        {
                            StoreId = storeId,
                            ProductId = product.Id.ToString(),
                            CategoryId = posCategoryId,
                            Price = product.Price,
                            Status = true,
                            Names = JsonConvert.SerializeObject(GetLocalizedNames(product.Localizations)),
                            Modifiers = product.ModifierGroups != null ? JsonConvert.SerializeObject(product.ModifierGroups) : null,
                            Tax = product.Taxes != null ? JsonConvert.SerializeObject(product.Taxes) : null,
                            CategoryIds = string.Join(",", product.CategoryIds ?? []),
                            CreatedBy = _currentUser.Id
                        }).ToList();
                
                    posProducts.AddRange(products);
                }
            }
        }
        
        await _posManagementDataProvider.AddPosProductsAsync(posProducts, true, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Sync products data: {@ProductsMap}", posProducts.ToDictionary(p => p.ProductId, p => p.Id));
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