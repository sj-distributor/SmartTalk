using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.Pos;

public partial interface IPosDataProvider : IScopedDependency
{
    Task<StoreCustomer> GetStoreCustomerAsync(int? id = null, string phone = null, CancellationToken cancellationToken = default);
    
    Task<(int, List<StoreCustomer>)> GetStoreCustomersAsync(int storeId, int? pageIndex = null, int? pageSize = null, string phone = null, CancellationToken cancellationToken = default);
    
    Task AddStoreCustomersAsync(List<StoreCustomer> customers, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateStoreCustomersAsync(List<StoreCustomer> customers, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class PosDataProvider : IPosDataProvider
{
    public async Task<StoreCustomer> GetStoreCustomerAsync(int? id = null, string phone = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<StoreCustomer>();

        if (id.HasValue)
            query = query.Where(x => x.Id == id.Value);

        if (!string.IsNullOrEmpty(phone))
            query = query.Where(x => x.Phone.Contains(phone));
        
        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int, List<StoreCustomer>)> GetStoreCustomersAsync(
        int storeId, int? pageIndex = null, int? pageSize = null, string phone = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<StoreCustomer>().Where(x => !x.IsDeleted && x.StoreId == storeId);
        
        if(!string.IsNullOrEmpty(phone))
            query = query.Where(x => x.Phone.Contains(phone));
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        var customers = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        
        return (count, customers);
    }

    public async Task AddStoreCustomersAsync(List<StoreCustomer> customers, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(customers, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateStoreCustomersAsync(List<StoreCustomer> customers, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(customers, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}