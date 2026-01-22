using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.Pos;

public partial interface IPosDataProvider : IScopedDependency
{
    Task CreatePosCompanyAsync(Company company, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task UpdatePosCompanyAsync(Company company, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task DeletePosCompanyAsync(Company company, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task<Company> GetPosCompanyAsync(int id, CancellationToken cancellationToken);

    Task<Company> GetPosCompanyByNameAsync(string name, CancellationToken cancellationToken);

    Task<List<int>> GetAssistantIdsByCompanyIdAsync(int companyId, CancellationToken cancellationToken = default);

    Task<List<PosMenu>> GetPosMenusAsync(int storeId, bool? IsActive = null, CancellationToken cancellationToken = default);

    Task UpdatePosMenuAsync(PosMenu menu, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task<PosMenu> GetPosMenuAsync(int? id = null, string menuId = null, CancellationToken cancellationToken = default);

    Task<List<PosCategory>> GetPosCategoriesAsync(int? menuId = null, int? id = null, int? storeId = null, List<int> ids = null, CancellationToken cancellationToken = default);

    Task<List<PosProduct>> GetPosProductsAsync(
        int? categoryId = null, string name = null, int? id = null, int? storeId = null, List<int> ids = null, List<int> categoryIds = null,
        List<string> productIds = null, string keyWord = null, bool? isActive = null, CancellationToken cancellationToken = default);

    Task UpdateCategoriesAsync(List<PosCategory> categories, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task UpdateProductsAsync(List<PosProduct> products, bool isForceSave = true, CancellationToken cancellationToken = default);
    
    Task<PosCategory> GetPosCategoryAsync(int id, CancellationToken cancellationToken);
    
    Task<PosProduct> GetPosProductAsync(int id, CancellationToken cancellationToken);
    
    Task<List<PosOrder>> GetPosOrdersByCompanyIdAsync(int companyId, CancellationToken cancellationToken);

    Task<List<PosOrder>> GetPosOrdersByStoreIdAsync(int storeId, CancellationToken cancellationToken);
    
    Task<List<PosProduct>> GetPosProductsByProductIdsAsync(int storeId, List<string> productIds, CancellationToken cancellationToken);
    
    Task<List<PosProduct>> GetPosProductsByAgentIdAsync(int agentId, CancellationToken cancellationToken);
}

public partial class PosDataProvider
{
    public async Task CreatePosCompanyAsync(Company company, bool isForceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(company, cancellationToken).ConfigureAwait(false);
        
        if (isForceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdatePosCompanyAsync(Company company, bool isForceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(company, cancellationToken).ConfigureAwait(false);
        
        if (isForceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePosCompanyAsync(Company company, bool isForceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(company, cancellationToken).ConfigureAwait(false);
        
        if (isForceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Company> GetPosCompanyAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.Query<Company>(x => x.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Company> GetPosCompanyByNameAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var normalizedName = name.Trim();

        return await _repository.Query<Company>().Where(x => x.Name == normalizedName).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<int>> GetAssistantIdsByCompanyIdAsync(int companyId, CancellationToken cancellationToken = default)
    {
        if (companyId <= 0) return [];

        var query = from store in _repository.Query<CompanyStore>().Where(x => x.CompanyId == companyId)
            join posAgent in _repository.Query<PosAgent>() on store.Id equals posAgent.StoreId
            join agentAssistant in _repository.Query<AgentAssistant>() on posAgent.AgentId equals agentAssistant.AgentId
            select agentAssistant.AssistantId;

        return await query.Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosMenu>> GetPosMenusAsync(int storeId, bool? IsActive = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PosMenu>(x => x.StoreId == storeId);
        
        if (IsActive.HasValue)
            query = query.Where(x => x.Status == IsActive.Value);
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<List<PosCategory>> GetPosCategoriesAsync(
        int? menuId = null, int? id = null, int? storeId = null, List<int> ids = null, CancellationToken cancellationToken = default)
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

        return await query.OrderBy(x => x.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosProduct>> GetPosProductsAsync(
        int? categoryId = null, string name = null, int? id = null, int? storeId = null, List<int> ids = null, List<int> categoryIds = null,
        List<string> productIds = null, string keyWord = null, bool? isActive = null, CancellationToken cancellationToken = default)
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

        if (categoryIds != null && categoryIds.Count != 0)
            query = query.Where(x => categoryIds.Contains(x.CategoryId));

        if (!string.IsNullOrEmpty(keyWord))
            query = query.Where(x => x.Names.Contains(keyWord) || x.Modifiers.Contains(keyWord));

        if (productIds != null && productIds.Count != 0)
            query = query.Where(x => productIds.Contains(x.ProductId));
        
        if (isActive.HasValue)
            query = query.Where(x => x.Status == isActive.Value);

        return await query.OrderBy(x => x.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<List<PosOrder>> GetPosOrdersByCompanyIdAsync(int companyId, CancellationToken cancellationToken)
    {
        var query = from company in _repository.Query<Company>()
            join store in _repository.Query<CompanyStore>() on company.Id equals store.CompanyId
            join order in _repository.Query<PosOrder>() on store.Id equals order.StoreId
            where company.Id == companyId
            select order;
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<PosOrder>> GetPosOrdersByStoreIdAsync(int storeId,CancellationToken cancellationToken)
    {
        var query = from store in _repository.Query<CompanyStore>()
            join order in _repository.Query<PosOrder>() on store.Id equals order.StoreId
            where store.Id == storeId
            select order;
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosProduct>> GetPosProductsByProductIdsAsync(int storeId, List<string> productIds, CancellationToken cancellationToken)
    {
        var query = from menu in _repository.Query<PosMenu>().Where(x => x.Status)
            join category in _repository.Query<PosCategory>() on menu.Id equals category.MenuId
            join product in _repository.Query<PosProduct>().Where(x => x.StoreId == storeId && productIds.Contains(x.ProductId)) on category.Id equals product.CategoryId
            select product;
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosProduct>> GetPosProductsByAgentIdAsync(int agentId, CancellationToken cancellationToken)
    {
        var query = from posAgent in _repository.Query<PosAgent>().Where(x => x.AgentId == agentId)
            join store in _repository.Query<CompanyStore>() on posAgent.StoreId equals store.Id
            join product in _repository.Query<PosProduct>() on store.Id equals product.StoreId
            select product;
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
