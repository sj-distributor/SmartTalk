using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementDataProvider
{
    Task<PosOrder> GetPosOrderByIdAsync(int orderId, CancellationToken cancellationToken = default);
    
    Task<List<PosOrder>> GetPosOrdersAsync(
        int storeId, string keyword= null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, CancellationToken cancellationToken = default);
    
    Task UpdatePosOrdersAsync(List<PosOrder> orders, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class PosManagementDataProvider
{
    public async Task<PosOrder> GetPosOrderByIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<PosOrder>().Where(x => x.Id == orderId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
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
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdatePosOrdersAsync(List<PosOrder> orders, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(orders, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}