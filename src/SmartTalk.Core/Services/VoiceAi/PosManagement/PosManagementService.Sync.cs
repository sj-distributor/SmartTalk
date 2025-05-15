using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
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
        
        var timePeriodsJson= JsonConvert.SerializeObject(posConfiguration.Data.TimePeriods);
        
        store.TimePeriod = timePeriodsJson;
        
        await _posManagementDataProvider.UpdateStoreAsync(store, cancellationToken).ConfigureAwait(false);
        
        var menus = posConfiguration.Data.Menus.Select(menu => new PosMenu
        {
            StoreId = store.Id,
            MenuId = menu.Id.ToString(),
            Names = JsonConvert.SerializeObject(GetLocalizedNames(menu.Localizations)),
            TimePeriod = JsonConvert.SerializeObject(menu.TimePeriods),
            CategoryIds = menu.CategoryIds == null ? string.Empty : string.Join(",", menu.CategoryIds),
            Status = menu.Status,
            CreatedBy = command.UserId,
            CreatedDate = DateTimeOffset.UtcNow,
            LastModifiedBy = command.UserId,
            LastModifiedDate = DateTimeOffset.UtcNow
        }).ToList();
    
        await _posManagementDataProvider.UpdateStoreMenusAsync(menus, cancellationToken).ConfigureAwait(false);

        foreach (var menu in posConfiguration.Data.Menus)
        {
            var categories = posConfiguration.Data.Categories
                .Where(c => c.MenuIds.Contains(menu.Id.ToString()))
                .Select(category => new PosCategory
                {
                    MenuId = menu.Id.ToString(),
                    CategoryId = category.Id.ToString(),
                    Names = JsonConvert.SerializeObject(GetLocalizedNames(category.Localizations)),
                    MenuIds = string.Join(",", category.MenuIds),
                    SortOrder = 0,
                    CreatedBy = command.UserId,
                    CreatedDate = DateTimeOffset.UtcNow,
                    LastModifiedBy = command.UserId,
                    LastModifiedDate = DateTimeOffset.UtcNow
                }).ToList();

            await _posManagementDataProvider.UpdateStoreCategoriesAsync(categories, cancellationToken).ConfigureAwait(false);
            
            foreach (var category in categories)
            {
                var products = posConfiguration.Data.Products
                    .Where(p => p.CategoryId.ToString() == category.CategoryId)
                    .Select(product => new PosProduct
                    {
                        ProductId = product.Id.ToString(),
                        CategoryId = product.CategoryId.ToString(),
                        Price = product.Price,
                        Status = true,
                        SortOrder = product.Sort,
                        Names = JsonConvert.SerializeObject(GetLocalizedNames(product.Localizations)),
                        Modifiers = product.ModifierGroups != null ? JsonConvert.SerializeObject(product.ModifierGroups) : null,
                        Tax = product.Taxes != null ? JsonConvert.SerializeObject(product.Taxes) : null,
                        CreatedBy = command.UserId,
                        CreatedDate = DateTimeOffset.UtcNow,
                        LastModifiedBy = command.UserId,
                        LastModifiedDate = DateTimeOffset.UtcNow
                    }).ToList();
                
                await _posManagementDataProvider.UpdateStoreProductsAsync(products, cancellationToken).ConfigureAwait(false);
            }
        }

        return new SyncPosConfigurationResponse
        {
            Data = posConfiguration
        };
    }
    
    private Dictionary<string, Dictionary<string, string>> GetLocalizedNames(IEnumerable<EasyPosResponseLocalization> localizations)
    {
        return localizations
            .Where(loc => !string.IsNullOrWhiteSpace(loc.LanguageCode) && !string.IsNullOrWhiteSpace(loc.Field))
            .GroupBy(loc => loc.LanguageCode.Substring(0, 2).ToLower())
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(loc => loc.Field, loc => loc.Value)
            );
    }
}