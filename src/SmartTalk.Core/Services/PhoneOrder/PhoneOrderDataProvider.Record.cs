using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderDataProvider
{
    Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(PhoneOrderRestaurant restaurant, CancellationToken cancellationToken);
}

public partial class PhoneOrderDataProvider
{
    public async Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(PhoneOrderRestaurant restaurant, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderRecord>(x => x.Restaurant == restaurant).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}