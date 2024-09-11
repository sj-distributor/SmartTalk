using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderDataProvider
{
    Task<List<PhoneOrderOrderItem>> GetPhoneOrderOrderItemsAsync(int recordId, CancellationToken cancellationToken);
}

public partial class PhoneOrderDataProvider
{
    public async Task<List<PhoneOrderOrderItem>> GetPhoneOrderOrderItemsAsync(int recordId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderOrderItem>(x => x.RecordId == recordId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}