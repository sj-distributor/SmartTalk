using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementDataProvider : IScopedDependency
{
    Task UpdateStoreAsync(PosCompanyStore store, bool forceSave = true, CancellationToken cancellationToken = default);

    Task AddPosMenusAsync(List<PosMenu> menus, bool forceSave = true, CancellationToken cancellationToken = default);

    Task AddPosCategoriesAsync(List<PosCategory> categories, bool forceSave = true, CancellationToken cancellationToken = default);

    Task AddPosProductsAsync(List<PosProduct> products, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task DeletePosMenuInfosAsync(int storeId, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class PosManagementDataProvider
{
    public async Task UpdateStoreAsync(PosCompanyStore store, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(store, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) 
            await _unitOfWork.SaveChangesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPosMenusAsync(List<PosMenu> menus, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(menus, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) 
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPosCategoriesAsync(List<PosCategory> categories, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(categories, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) 
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPosProductsAsync(List<PosProduct> products, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(products, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) 
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePosMenuInfosAsync(int storeId, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await DeletePosMenusAsync(storeId, forceSave, cancellationToken).ConfigureAwait(false);
        await DeletePosCategoriesAsync(storeId, forceSave, cancellationToken).ConfigureAwait(false);
        await DeletePosProductsAsync(storeId, forceSave, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task DeletePosMenusAsync(int storeId, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        var menus = await _repository.Query<PosMenu>().Where(x => x.StoreId == storeId).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (menus.Count != 0)
        {
            await _repository.DeleteAllAsync(menus, cancellationToken).ConfigureAwait(false);
            
            if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DeletePosCategoriesAsync(int storeId, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        var categories = await _repository.Query<PosCategory>().Where(x => x.StoreId == storeId).ToListAsync(cancellationToken).ConfigureAwait(false);
        
        if (categories.Count != 0)
        {
            await _repository.DeleteAllAsync(categories, cancellationToken).ConfigureAwait(false);
            
            if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DeletePosProductsAsync(int storeId, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        var products = await _repository.Query<PosProduct>().Where(x => x.StoreId == storeId).ToListAsync(cancellationToken).ConfigureAwait(false);
        
        if (products.Count != 0)
        {
            await _repository.DeleteAllAsync(products, cancellationToken).ConfigureAwait(false);
            
            if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}