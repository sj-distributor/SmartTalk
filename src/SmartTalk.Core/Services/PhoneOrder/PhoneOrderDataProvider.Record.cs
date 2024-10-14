using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
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
    
    Task<List<GetPhoneOrderRecordsForRestaurantCountDto>> GetPhoneOrderRecordsForRestaurantCountAsync(
        DateTimeOffset dayShiftTime, DateTimeOffset nightShiftTime, DateTimeOffset endTime, CancellationToken cancellationToken);

    Task<List<GetPhoneOrderRecordsWithUserCountDto>> GetPhoneOrderRecordsWithUserCountAsync(
        DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken);
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
                where record.Status == PhoneOrderRecordStatus.Sent
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
                    ManualOrderId = record.ManualOrderId,
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

    public async Task<List<GetPhoneOrderRecordsForRestaurantCountDto>> GetPhoneOrderRecordsForRestaurantCountAsync(
        DateTimeOffset dayShiftTime, DateTimeOffset nightShiftTime, DateTimeOffset endTime, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderRecord>()
            .Where(x => x.Status == PhoneOrderRecordStatus.Sent)
            .GroupBy(x => x.Restaurant)
            .Select(restaurantGroup => new GetPhoneOrderRecordsForRestaurantCountDto
            {
                Restaurant = restaurantGroup.Key,
                Classes = new List<RestaurantCountDto>
                {
                    new()
                    {
                        TimeFrame = "夜班",
                        Count = restaurantGroup.Count(x => x.CreatedDate >= dayShiftTime && x.CreatedDate <= nightShiftTime)
                    },
                    new()
                    {
                        TimeFrame = "日班",
                        Count = restaurantGroup.Count(x => x.CreatedDate >= nightShiftTime && x.CreatedDate < endTime)
                    }
                }
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async  Task<List<GetPhoneOrderRecordsWithUserCountDto>> GetPhoneOrderRecordsWithUserCountAsync(
        DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderRecord>()
            .Where(x => x.LastModifiedBy.HasValue)
            .Where(x => x.Status == PhoneOrderRecordStatus.Sent)
            .Where(x => x.LastModifiedDate >= startTime && x.LastModifiedDate < endTime)
            .Join(_repository.Query<UserAccount>(), x => x.LastModifiedBy, s => s.Id, (record, account) => new
            {
                UserName = account.UserName,
                record
            })
            .GroupBy(x => x.UserName)
            .Select(g => new GetPhoneOrderRecordsWithUserCountDto
            {
                UserName = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.UserName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}