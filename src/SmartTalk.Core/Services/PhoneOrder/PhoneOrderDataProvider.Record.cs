using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderDataProvider
{
    Task AddPhoneOrderRecordsAsync(List<PhoneOrderRecord> phoneOrderRecords, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(PhoneOrderRestaurant restaurant, CancellationToken cancellationToken);
}

public partial class PhoneOrderDataProvider
{
    public async Task AddPhoneOrderRecordsAsync(List<PhoneOrderRecord> phoneOrderRecords, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (phoneOrderRecords == null || phoneOrderRecords.Count == 0) return;

        await _repository.InsertAllAsync(phoneOrderRecords, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(PhoneOrderRestaurant restaurant, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderRecord>(x => x.Restaurant == restaurant).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}