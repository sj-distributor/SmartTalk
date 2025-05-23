using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementDataProvider : IScopedDependency
{
    Task UpdateStoreAsync(PosCompanyStore store, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateStoreMenusAsync(List<PosMenu> menus, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateStoreCategoriesAsync(List<PosCategory> categories, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateStoreProductsAsync(List<PosProduct> products, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class PosManagementDataProvider
{
    public async Task UpdateStoreAsync(PosCompanyStore store, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(store, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) 
            await _unitOfWork.SaveChangesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateStoreMenusAsync(List<PosMenu> menus, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(menus, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) 
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateStoreCategoriesAsync(List<PosCategory> categories, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(categories, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) 
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateStoreProductsAsync(List<PosProduct> products, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(products, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) 
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}