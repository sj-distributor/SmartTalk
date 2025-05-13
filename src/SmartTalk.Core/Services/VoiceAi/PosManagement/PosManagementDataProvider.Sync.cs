using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.EasyPos;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementDataProvider : IScopedDependency
{
    Task UpdateStoreTimePeriodsAsync(int storeId, List<EasyPosResponseTimePeriod> timePeriods, CancellationToken cancellationToken);

    Task UpdateStoreMenusAsync(int storeId, List<EasyPosResponseMenu> menus, int userId, CancellationToken cancellationToken);

    Task UpdateStoreCategoriesAsync(string menuId, List<EasyPosResponseCategory> categories, int userId, CancellationToken cancellationToken);

    Task UpdateStoreProductsAsync(int categoryId, List<EasyPosResponseProduct> products, int userId, CancellationToken cancellationToken);
}

public partial class PosManagementDataProvider
{
    public async Task UpdateStoreTimePeriodsAsync(int storeId, List<EasyPosResponseTimePeriod> timePeriods, CancellationToken cancellationToken)
    {
        var store = await _repository.GetByIdAsync<PosCompanyStore>(storeId, cancellationToken).ConfigureAwait(false);
        
        if (store == null) throw new Exception($"Store with ID {storeId} not found.");
        
        var timePeriodsJson= JsonConvert.SerializeObject(timePeriods);
        
        store.TimePeriod = timePeriodsJson;
        
        await _repository.UpdateAsync(store, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateStoreMenusAsync(int storeId, List<EasyPosResponseMenu> menus, int userId, CancellationToken cancellationToken)
    {
        var existingMenus = await _repository.Query<PosMenu>().Where(m => m.StoreId == storeId).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (existingMenus.Any()) await _repository.DeleteAllAsync(existingMenus, cancellationToken).ConfigureAwait(false);
        
        var newMenus = menus.Select(menu => new PosMenu
        {
            StoreId = storeId,
            MenuId = menu.Id,
            TimePeriod = JsonConvert.SerializeObject(menu.TimePeriods),
            CategoryIds = menu.CategoryIds ?? string.Empty,
            Status = menu.Status,
            CreatedBy = userId,
            CreatedDate = DateTimeOffset.UtcNow,
            LastModifiedBy = userId,
            LastModifiedDate = DateTimeOffset.UtcNow
        }).ToList();
        
        await _repository.InsertAllAsync(newMenus, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateStoreCategoriesAsync(string menuId, List<EasyPosResponseCategory> categories, int userId, CancellationToken cancellationToken)
    {
        var existingCategories = await _repository.Query<PosCategory>().Where(c => c.MenuId == menuId).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (existingCategories.Any()) await _repository.DeleteAllAsync(existingCategories, cancellationToken).ConfigureAwait(false);
        
        var newCategories = categories.Select(category =>
        {
            var nameLocalizations = category.Localizations.Where(loc => loc.Field == "name")
                .Select(loc =>
                    string.IsNullOrWhiteSpace(loc.LanguageCode) || string.IsNullOrWhiteSpace(loc.Value)
                        ? null
                        : $"{loc.LanguageCode}:{loc.Value}").Where(x => x != null);

            return new PosCategory
            {
                MenuId = menuId,
                CategoryId = category.Id,
                Names = string.Join(" | ", nameLocalizations),
                MenuIds = string.Join(",", category.MenuIds.Select(m => m.ToString())),
                SortOrder = 0,
                CreatedBy = userId,
                CreatedDate = DateTimeOffset.UtcNow,
                LastModifiedBy = userId,
                LastModifiedDate = DateTimeOffset.UtcNow
            };
        }).ToList();
        
        await _repository.InsertAllAsync(newCategories, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateStoreProductsAsync(int categoryId, List<EasyPosResponseProduct> products, int userId, CancellationToken cancellationToken)
    {
        var existingProducts = await _repository.Query<PosProduct>().Where(c => c.CategoryId == categoryId).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (existingProducts.Any()) await _repository.DeleteAllAsync(existingProducts, cancellationToken).ConfigureAwait(false);
        
        var newProducts = products.Select(p =>
        {
            var nameLocalizations = p.Localizations?
                .Where(loc => loc.Field == "name")
                .Select(loc => $"{loc.LanguageCode}:{loc.Value}");

            return new PosProduct
            {
                ProductId = p.Id.ToString(),
                CategoryId = p.CategoryId,
                Price = p.Price,
                Status = true,
                SortOrder = p.SortOrder,
                Names = nameLocalizations != null ? string.Join(" | ", nameLocalizations) : null,
                Modifiers = p.ModifierGroups != null ? JsonConvert.SerializeObject(p.ModifierGroups) : null,
                Tax = p.Taxes != null ? JsonConvert.SerializeObject(p.Taxes) : null,
                CreatedBy = userId,
                CreatedDate = DateTimeOffset.UtcNow,
                LastModifiedBy = userId,
                LastModifiedDate = DateTimeOffset.UtcNow
            };
        }).ToList();

        await _repository.InsertAllAsync(newProducts, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}