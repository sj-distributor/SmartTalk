using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Account;
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
        var query = from record in _repository.Query<PhoneOrderRecord>()
                join user in _repository.Query<UserAccount>() on record.LastModifiedBy equals user.Id into userGroup
                from user in userGroup.DefaultIfEmpty()
                where record.Restaurant == restaurant
                orderby record.CreatedDate descending 
                select new PhoneOrderRecord
                {
                    Id = record.Id,
                    SessionId = record.SessionId,
                    Restaurant = record.Restaurant,
                    Tips = record.Tips,
                    TranscriptionText = record.TranscriptionText,
                    Url = record.Url,
                    LastModifiedBy = record.LastModifiedBy,
                    CreatedDate = record.CreatedDate,
                    UserAccount = user
                };

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}