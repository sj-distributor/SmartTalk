using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

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
        
        await _posManagementDataProvider.UpdateStoreTimePeriodsAsync(command.StoreId, posConfiguration.Data.TimePeriods, cancellationToken);
    
        await _posManagementDataProvider.UpdateStoreMenusAsync(command.StoreId, posConfiguration.Data.Menus, command.UserId, cancellationToken);

        foreach (var menu in posConfiguration.Data.Menus)
        {
            var categories = posConfiguration.Data.Categories.Where(c => c.MenuIds.Contains(menu.Id)).ToList();

            await _posManagementDataProvider.UpdateStoreCategoriesAsync(menu.Id, categories, command.UserId, cancellationToken);
            
            foreach (var category in categories)
            {
                if (!int.TryParse(category.Id, out var categoryIdInt))
                {
                    Log.Warning("category.Id 转换失败: {CategoryId}", category.Id);
                    continue;
                }
                
                var products = posConfiguration.Data.Products.Where(p => p.CategoryId == categoryIdInt).ToList();
                
                await _posManagementDataProvider.UpdateStoreProductsAsync(categoryIdInt, products, command.UserId, cancellationToken);
            }
        }

        return new SyncPosConfigurationResponse
        {
            Data = posConfiguration
        };
    }
}