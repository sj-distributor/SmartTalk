using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderDataProvider
{
    Task<List<PhoneOrderOrderItem>> GetPhoneOrderOrderItemsAsync(int RecordId, CancellationToken cancellationToken);

    Task AddPhoneOrderOrderItemsAsync(List<PhoneOrderOrderItem> orderItems, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class PhoneOrderDataProvider
{
    public async Task<List<PhoneOrderOrderItem>> GetPhoneOrderOrderItemsAsync(int RecordId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderOrderItem>(x => x.RecordId == RecordId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPhoneOrderOrderItemsAsync(List<PhoneOrderOrderItem> orderItems, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (!orderItems.Any()) return;

        await _repository.InsertAllAsync(orderItems, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}