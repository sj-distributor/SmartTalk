using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Enums.Pos;

namespace SmartTalk.Core.Services.Pos;

public partial interface IPosDataProvider
{
    Task<PosOrder> GetPosOrderByIdAsync(int? orderId = null, string posOrderId = null, int? recordId = null, CancellationToken cancellationToken = default);
    
    Task<List<PosOrder>> GetPosOrdersAsync(
        int storeId, string keyword= null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, CancellationToken cancellationToken = default);
    
    Task UpdatePosOrdersAsync(List<PosOrder> orders, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task AddPosOrdersAsync(List<PosOrder> orders, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<PosOrder> GetPosOrderSortByOrderNoAsync(int storeId, DateTimeOffset utcStart, DateTimeOffset utcEnd, CancellationToken cancellationToken);

    Task<List<PosOrder>>GetPosOrdersByStoreIdsAsync(
        List<int> storeIds, PosOrderModifiedStatus? modifiedStatus = null, bool? isPush = null, DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null, CancellationToken cancellationToken = default);
    
    Task<List<PosOrder>> GetPosCustomerInfosAsync(CancellationToken cancellationToken);
    
    Task<List<PosOrder>> GetPosOrdersByRecordIdsAsync(List<int >recordId, CancellationToken cancellationToken = default);
}

public partial class PosDataProvider
{
    public async Task<PosOrder> GetPosOrderByIdAsync(int? orderId = null, string posOrderId = null, int? recordId = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PosOrder>();

        if (orderId.HasValue)
            query = query.Where(x => x.Id == orderId.Value);
        
        if (!string.IsNullOrEmpty(posOrderId))
            query = query.Where(x => x.OrderId == posOrderId);
        
        if (recordId.HasValue)
            query = query.Where(x => x.RecordId == recordId.Value);
        
        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosOrder>> GetPosOrdersAsync(
        int storeId, string keyword= null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PosOrder>().Where(x => x.StoreId == storeId);
        
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.OrderNo.Contains(keyword) || x.Phone.Contains(keyword));

        if (startDate.HasValue)
            query = query.Where(x => x.CreatedDate >= startDate.Value);
        
        if (endDate.HasValue)
            query = query.Where(x => x.CreatedDate <= endDate.Value);
        
        return await query.OrderByDescending(x => x.CreatedDate).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdatePosOrdersAsync(List<PosOrder> orders, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(orders, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPosOrdersAsync(List<PosOrder> orders, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(orders, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<PosOrder> GetPosOrderSortByOrderNoAsync(int storeId, DateTimeOffset utcStart, DateTimeOffset utcEnd, CancellationToken cancellationToken)
    {
        return await _repository.Query<PosOrder>()
            .Where(x => x.StoreId == storeId && x.CreatedDate >= utcStart && x.CreatedDate < utcEnd)
            .OrderByDescending(x => x.OrderNo).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<PosOrder>> GetPosOrdersByStoreIdsAsync(
        List<int> storeIds, PosOrderModifiedStatus? modifiedStatus = null, bool? isPush = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PosOrder>().Where(x => storeIds.Contains(x.StoreId) && x.OrderId != null);
        
        if (modifiedStatus.HasValue)
            query = query.Where(x => x.ModifiedStatus == modifiedStatus.Value);
        
        if (isPush.HasValue)
            query = query.Where(x => x.IsPush == isPush.Value);
        
        if (startDate.HasValue)
            query = query.Where(x => x.CreatedDate >= startDate.Value);
        
        if (endDate.HasValue)
            query = query.Where(x => x.CreatedDate <= endDate.Value);
        
        return await query.OrderByDescending(x => x.CreatedDate).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<PosOrder>> GetPosCustomerInfosAsync(CancellationToken cancellationToken)
    {
        var query = _repository.QueryNoTracking<PosOrder>().Where(x => !string.IsNullOrWhiteSpace(x.Phone));
        
        var latestOrders = await query
            .GroupBy(x => new { x.Phone, x.Type })
            .Select(g => g.OrderByDescending(x => x.CreatedDate).First())
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return latestOrders;
    }

    public async Task<List<PosOrder>> GetPosOrdersByRecordIdsAsync(List<int> recordIds, CancellationToken cancellationToken = default)
    {
        if (recordIds == null || recordIds.Count == 0) return new List<PosOrder>();

        return await _repository.Query<PosOrder>().Where(x => x.RecordId.HasValue && recordIds.Contains(x.RecordId.Value) && x.Status == PosOrderStatus.Pending).ToListAsync(cancellationToken);
    }
}