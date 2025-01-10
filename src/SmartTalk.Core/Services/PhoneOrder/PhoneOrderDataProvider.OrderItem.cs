using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.PhoneCall;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderDataProvider
{
    Task<List<PhoneCallOrderItem>> GetPhoneOrderOrderItemsAsync(int recordId, PhoneOrderOrderType? type = null, CancellationToken cancellationToken = default);

    Task DeletePhoneOrderItemsAsync(List<PhoneCallOrderItem> items, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class PhoneOrderDataProvider
{
    public async Task<List<PhoneCallOrderItem>> GetPhoneOrderOrderItemsAsync(int recordId, PhoneOrderOrderType? type = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PhoneCallOrderItem>(x => x.RecordId == recordId);

        if (type.HasValue)
            query = query.Where(x => x.OrderType == type.Value);

        return  await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePhoneOrderItemsAsync(List<PhoneCallOrderItem> items, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(items, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}