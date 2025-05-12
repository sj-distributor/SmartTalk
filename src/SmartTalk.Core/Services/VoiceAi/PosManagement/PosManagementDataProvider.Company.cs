using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementDataProvider : IScopedDependency
{
    Task CreatePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task UpdatePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task DeletePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task<PosCompany> GetPosCompanyAsync(int id, CancellationToken cancellationToken);

    Task<List<PosMenu>> GetPosMenusAsync(int storeId, CancellationToken cancellationToken);

    Task UpdatePosMenuAsync(PosMenu menu, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task<PosMenu> GetPosMenuAsync(string menuId, int? id, CancellationToken cancellationToken);

    Task<List<PosCategory>> GetPosCategoriesAsync(int? menuId, int? id, CancellationToken cancellationToken);

    Task<List<PosProduct>> GetPosProductsAsync(int? categoryId, int? id, string name, CancellationToken cancellationToken);

    Task UpdateCategoriesAsync(List<PosCategory> categories, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task UpdateProductsAsync(List<PosProduct> products, bool isForceSave = true, CancellationToken cancellationToken = default);
}

public partial class PosManagementDataProvider : IPosManagementDataProvider
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

    public async Task<PosMenu> GetPosMenuAsync(string menuId, int? id,  CancellationToken cancellationToken)
    {
        var query = _repository.Query<PosMenu>();

        if (menuId.Any())
            query = query.Where(x => x.MenuId == menuId);

        if (id.HasValue)
            query = query.Where(x => x.Id == id);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosCategory>> GetPosCategoriesAsync(int? menuId, int? id, CancellationToken cancellationToken)
    {
        var query = _repository.Query<PosCategory>();

        if (menuId.HasValue)
            query = query.Where(x => x.MenuId == menuId);

        if (id.HasValue)
            query = query.Where(x => x.Id == id);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosProduct>> GetPosProductsAsync(int? categoryId, int? id, string name,  CancellationToken cancellationToken)
    {
        var query = _repository.Query<PosProduct>();

        if (categoryId.HasValue)
            query = query.Where(x => x.CategoryId == categoryId);

        if (id.HasValue)
            query = query.Where(x => x.Id == id);

        if (!string.IsNullOrEmpty(name))
            query = query.Where(x => x.Names.Contains(name));

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosCategory>> GetPosCategoriesAsync(int menuId, int categoryId,  CancellationToken cancellationToken)
    {
        return await _repository.Query<PosCategory>(x => x.MenuId == menuId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosProduct>> GetPosProductsAsync(int categoryId, int productId,  CancellationToken cancellationToken)
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
}