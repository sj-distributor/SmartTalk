using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.PhoneCall;
using SmartTalk.Core.Domain.SpeechMatics;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.SpeechMatics;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderDataProvider
{
    Task AddPhoneCallRecordsAsync(List<PhoneCallRecord> PhoneCallRecords, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<PhoneCallRecord>> GetPhoneCallRecordsAsync(PhoneOrderRestaurant restaurant, CancellationToken cancellationToken);

    Task AddPhoneOrderItemAsync(List<PhoneCallOrderItem> phoneOrderOrderItems, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdatePhoneCallRecordsAsync(PhoneCallRecord record, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<PhoneCallRecord>> GetPhoneCallRecordAsync(int? recordId = null, DateTimeOffset? createdDate = null, CancellationToken cancellationToken = default);

    Task<PhoneCallRecord> GetPhoneCallRecordByTranscriptionJobIdAsync(string transcriptionJobId, CancellationToken cancellationToken = default);
    
    Task<List<GetPhoneCallRecordsForRestaurantCountDto>> GetPhoneCallRecordsForRestaurantCountAsync(
        DateTimeOffset dayShiftTime, DateTimeOffset nightShiftTime, DateTimeOffset endTime, CancellationToken cancellationToken);

    Task<List<GetPhoneCallRecordsWithUserCountDto>> GetPhoneCallRecordsWithUserCountAsync(
        DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken);
}

public partial class PhoneOrderDataProvider
{
    public async Task AddPhoneCallRecordsAsync(List<PhoneCallRecord> PhoneCallRecords, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (PhoneCallRecords == null || PhoneCallRecords.Count == 0) return;

        await _repository.InsertAllAsync(PhoneCallRecords, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PhoneCallRecord>> GetPhoneCallRecordsAsync(PhoneOrderRestaurant restaurant, CancellationToken cancellationToken)
    {
        var query = _repository.Query<PhoneCallRecord>()
            .Where(record => record.Restaurant == restaurant && record.Status == PhoneOrderRecordStatus.Sent)
            .OrderByDescending(record => record.CreatedDate);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdatePhoneCallRecordsAsync(PhoneCallRecord record, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PhoneCallRecord>> GetPhoneCallRecordAsync(
        int? recordId = null, DateTimeOffset? createdDate = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PhoneCallRecord>();

        if (recordId.HasValue)
            query = query.Where(x => x.Id == recordId);

        if (createdDate.HasValue)
            query = query.Where(x => x.CreatedDate == createdDate && x.Status == PhoneOrderRecordStatus.Sent);
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPhoneOrderItemAsync(
        List<PhoneCallOrderItem> phoneOrderOrderItems, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(phoneOrderOrderItems, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<PhoneCallRecord> GetPhoneCallRecordByTranscriptionJobIdAsync(string transcriptionJobId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<PhoneCallRecord>().Where(x => x.TranscriptionJobId == transcriptionJobId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<GetPhoneCallRecordsForRestaurantCountDto>> GetPhoneCallRecordsForRestaurantCountAsync(
        DateTimeOffset dayShiftTime, DateTimeOffset nightShiftTime, DateTimeOffset endTime, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneCallRecord>()
            .Where(x => x.Status == PhoneOrderRecordStatus.Sent)
            .GroupBy(x => x.Restaurant)
            .Select(restaurantGroup => new GetPhoneCallRecordsForRestaurantCountDto
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

    public async Task<List<GetPhoneCallRecordsWithUserCountDto>> GetPhoneCallRecordsWithUserCountAsync(
        DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneCallRecord>()
            .Where(x => x.LastModifiedBy.HasValue)
            .Where(x => x.Status == PhoneOrderRecordStatus.Sent)
            .Where(x => x.LastModifiedDate >= startTime && x.LastModifiedDate < endTime)
            .Join(_repository.Query<UserAccount>(), x => x.LastModifiedBy, s => s.Id, (record, account) => new
            {
                UserName = account.UserName,
                record
            })
            .GroupBy(x => x.UserName)
            .Select(g => new GetPhoneCallRecordsWithUserCountDto
            {
                UserName = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.UserName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}