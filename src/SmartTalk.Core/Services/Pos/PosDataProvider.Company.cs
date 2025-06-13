using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.Pos;

public partial interface IPosDataProvider : IScopedDependency
{
    Task CreatePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task UpdatePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task DeletePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task<PosCompany> GetPosCompanyAsync(int id, CancellationToken cancellationToken);

    Task<List<PosMenu>> GetPosMenusAsync(int storeId, CancellationToken cancellationToken);

    Task UpdatePosMenuAsync(PosMenu menu, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task<PosMenu> GetPosMenuAsync(int? id = null, string menuId = null, CancellationToken cancellationToken = default);

    Task<List<PosCategory>> GetPosCategoriesAsync(int? menuId = null, int? id = null, int? storeId = null, List<int> ids = null, CancellationToken cancellationToken = default);

    Task<List<PosProduct>> GetPosProductsAsync(
        int? categoryId = null, string name = null, int? id = null, int? storeId = null, List<int> ids = null, List<string> productIds = null, string keyWord = null, CancellationToken cancellationToken = default);

    Task UpdateCategoriesAsync(List<PosCategory> categories, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task UpdateProductsAsync(List<PosProduct> products, bool isForceSave = true, CancellationToken cancellationToken = default);
    
    Task<PosCategory> GetPosCategoryAsync(int id, CancellationToken cancellationToken);
    
    Task<PosProduct> GetPosProductAsync(int id, CancellationToken cancellationToken);
}

public partial class PosDataProvider
{
    public async Task CreatePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(company, cancellationToken).ConfigureAwait(false);
        
        if (isForceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdatePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(company, cancellationToken).ConfigureAwait(false);
        
        if (isForceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(company, cancellationToken).ConfigureAwait(false);
        
        if (isForceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PosCompany> GetPosCompanyAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.Query<PosCompany>(x => x.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosMenu>> GetPosMenusAsync(int storeId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PosMenu>(x => x.StoreId == storeId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdatePosMenuAsync(PosMenu menu, bool isForceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(menu, cancellationToken).ConfigureAwait(false);
        
        if (isForceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PosMenu> GetPosMenuAsync(int? id = null, string menuId = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PosMenu>();

        if (!string.IsNullOrEmpty(menuId))
            query = query.Where(x => x.MenuId == menuId);

        if (id.HasValue)
            query = query.Where(x => x.Id == id.Value);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosCategory>> GetPosCategoriesAsync(int? menuId = null, int? id = null, int? storeId = null, List<int> ids = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PosCategory>();

        if (menuId.HasValue)
            query = query.Where(x => x.MenuId == menuId.Value);

        if (id.HasValue)
            query = query.Where(x => x.Id == id.Value);

        if (storeId.HasValue)
            query = query.Where(x => x.StoreId == storeId.Value);

        if (ids != null && ids.Count != 0)
            query = query.Where(x => ids.Contains(x.Id));

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosProduct>> GetPosProductsAsync(
        int? categoryId = null, string name = null, int? id = null, int? storeId = null, List<int> ids = null, List<string> productIds = null, string keyWord = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PosProduct>();

        if (categoryId.HasValue)
            query = query.Where(x => x.CategoryId == categoryId.Value);

        if (id.HasValue)
            query = query.Where(x => x.Id == id.Value);

        if (!string.IsNullOrEmpty(name))
            query = query.Where(x => x.Names.Contains(name));
        
        if (storeId.HasValue)
            query = query.Where(x => x.StoreId == storeId.Value);

        if (ids != null && ids.Count != 0)
            query = query.Where(x => ids.Contains(x.Id));

        if (!string.IsNullOrEmpty(keyWord))
            query = query.Where(x => x.Names.Contains(keyWord) || x.Modifiers.Contains(keyWord));

        if (productIds != null && productIds.Count != 0)
            query = query.Where(x => productIds.Contains(x.ProductId));

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosCategory>> GetPosCategoriesAsync(int menuId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PosCategory>(x => x.MenuId == menuId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosProduct>> GetPosProductsAsync(int categoryId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PosProduct>(x => x.CategoryId == categoryId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateCategoriesAsync(List<PosCategory> categories, bool isForceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(categories, cancellationToken).ConfigureAwait(false);
        
        if (isForceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateProductsAsync(List<PosProduct> products, bool isForceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(products, cancellationToken).ConfigureAwait(false);
        
        if (isForceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PosCategory> GetPosCategoryAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.Query<PosCategory>(x => x.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PosProduct> GetPosProductAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.Query<PosProduct>(x => x.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}