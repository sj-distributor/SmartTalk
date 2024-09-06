using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderDataProvider
{
    Task AddPhoneOrderRecordsAsync(List<PhoneOrderRecord> phoneOrderRecords, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(PhoneOrderRestaurant restaurant, CancellationToken cancellationToken);

    Task AddPhoneOrderItemAsync(List<PhoneOrderOrderItem> phoneOrderOrderItems, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdatePhoneOrderRecordsAsync(PhoneOrderRecord record, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<PhoneOrderRecord> GetPhoneOrderRecordByIdAsync(int recordId, CancellationToken cancellationToken);

    Task<PhoneOrderRecord> GetPhoneOrderRecordByTranscriptionJobIdAsync(string transcriptionJobId, CancellationToken cancellationToken = default);
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

    public async Task UpdatePhoneOrderRecordsAsync(PhoneOrderRecord record, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PhoneOrderRecord> GetPhoneOrderRecordByIdAsync(int recordId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderRecord>().Where(x => x.Id == recordId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPhoneOrderItemAsync(
        List<PhoneOrderOrderItem> phoneOrderOrderItems, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(phoneOrderOrderItems, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<PhoneOrderRecord> GetPhoneOrderRecordByTranscriptionJobIdAsync(string transcriptionJobId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<PhoneOrderRecord>().Where(x => x.TranscriptionJobId == transcriptionJobId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}