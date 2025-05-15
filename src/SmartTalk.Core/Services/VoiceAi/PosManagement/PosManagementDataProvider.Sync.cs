using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementDataProvider : IScopedDependency
{
    Task UpdateStoreAsync(PosCompanyStore store, CancellationToken cancellationToken);

    Task UpdateStoreMenusAsync(List<PosMenu> menus, CancellationToken cancellationToken);

    Task UpdateStoreCategoriesAsync(List<PosCategory> categories, CancellationToken cancellationToken);

    Task UpdateStoreProductsAsync(List<PosProduct> products, CancellationToken cancellationToken);
}

public partial class PosManagementDataProvider
{
    public async Task UpdateStoreAsync(PosCompanyStore store, CancellationToken cancellationToken)
    {
        await _repository.UpdateAsync(store, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateStoreMenusAsync(List<PosMenu> menus, CancellationToken cancellationToken)
    {
        await _repository.UpdateAllAsync(menus, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateStoreCategoriesAsync(List<PosCategory> categories, CancellationToken cancellationToken)
    {
        await _repository.UpdateAllAsync(categories, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateStoreProductsAsync(List<PosProduct> products, CancellationToken cancellationToken)
    {
        await _repository.UpdateAllAsync(products, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}