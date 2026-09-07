using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Notification;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Requests.Notification;

namespace SmartTalk.Core.Services.Notification;

public interface ICallNotificationDataProvider : IScopedDependency
{
    Task<CallNotificationScenarioRule> GetScenarioRuleAsync(string scenarioKey, bool activeOnly, CancellationToken cancellationToken);
    Task<List<CallNotificationScenarioRule>> GetScenarioRulesAsync(CancellationToken cancellationToken);
    Task<List<StoreCallNotificationSetting>> GetStoreSettingsAsync(int storeId, CancellationToken cancellationToken);
    Task<StoreCallNotificationSetting> GetStoreSettingAsync(int storeId, string scenarioKey, CancellationToken cancellationToken);
    ValueTask<CallNotificationRecord> GetRecordAsync(int id, CancellationToken cancellationToken);
    Task<CallNotificationRecord> GetRecordByCallAsync(int phoneOrderRecordId, CancellationToken cancellationToken);
    Task<string> GetOriginReportAsync(int recordId, CancellationToken cancellationToken);
    Task<(int Count, List<CallNotificationRecordDto> Records)> GetRecordsAsync(GetCallNotificationRecordsRequest request, List<int> storeIds, bool allStores, bool includePaging, CancellationToken cancellationToken);
    Task AddAsync(CallNotificationRecord record, CancellationToken cancellationToken);
    Task AddAsync(CallNotificationScenarioRule rule, CancellationToken cancellationToken);
    Task AddAsync(StoreCallNotificationSetting setting, CancellationToken cancellationToken);
    Task UpdateAsync(CallNotificationRecord record, CancellationToken cancellationToken);
    Task UpdateAsync(CallNotificationScenarioRule rule, CancellationToken cancellationToken);
    Task UpdateAsync(StoreCallNotificationSetting setting, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public class CallNotificationDataProvider : ICallNotificationDataProvider
{
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CallNotificationDataProvider(IRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public Task<CallNotificationScenarioRule> GetScenarioRuleAsync(string scenarioKey, bool activeOnly, CancellationToken cancellationToken) =>
        _repository.QueryNoTracking<CallNotificationScenarioRule>()
            .Where(x => x.ScenarioKey == scenarioKey && (!activeOnly || x.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<List<CallNotificationScenarioRule>> GetScenarioRulesAsync(CancellationToken cancellationToken) =>
        _repository.QueryNoTracking<CallNotificationScenarioRule>().ToListAsync(cancellationToken);

    public Task<List<StoreCallNotificationSetting>> GetStoreSettingsAsync(int storeId, CancellationToken cancellationToken) =>
        _repository.QueryNoTracking<StoreCallNotificationSetting>().Where(x => x.StoreId == storeId).ToListAsync(cancellationToken);

    public Task<StoreCallNotificationSetting> GetStoreSettingAsync(int storeId, string scenarioKey, CancellationToken cancellationToken) =>
        _repository.Query<StoreCallNotificationSetting>().SingleOrDefaultAsync(x => x.StoreId == storeId && x.ScenarioKey == scenarioKey, cancellationToken);

    public ValueTask<CallNotificationRecord> GetRecordAsync(int id, CancellationToken cancellationToken) =>
        _repository.GetByIdAsync<CallNotificationRecord>(id, cancellationToken);

    public Task<CallNotificationRecord> GetRecordByCallAsync(int phoneOrderRecordId, CancellationToken cancellationToken) =>
        _repository.Query<CallNotificationRecord>().SingleOrDefaultAsync(x => x.PhoneOrderRecordId == phoneOrderRecordId && x.Channel == "sms", cancellationToken);

    public Task<string> GetOriginReportAsync(int recordId, CancellationToken cancellationToken) =>
        _repository.QueryNoTracking<PhoneOrderRecordReport>()
            .Where(x => x.RecordId == recordId && x.IsOrigin)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => x.Report)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(int Count, List<CallNotificationRecordDto> Records)> GetRecordsAsync(GetCallNotificationRecordsRequest request, List<int> storeIds, bool allStores, bool includePaging, CancellationToken cancellationToken)
    {
        var query = _repository.QueryNoTracking<CallNotificationRecord>();
        if (!allStores) query = query.Where(x => storeIds.Contains(x.StoreId));
        if (request.CompanyId.HasValue) query = query.Where(x => x.CompanyId == request.CompanyId.Value);
        if (request.StoreId.HasValue) query = query.Where(x => x.StoreId == request.StoreId.Value);
        if (!string.IsNullOrWhiteSpace(request.ScenarioKey)) query = query.Where(x => x.ScenarioKey == request.ScenarioKey);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.CustomerNumberKeyword)) query = query.Where(x => x.CustomerNumber.Contains(request.CustomerNumberKeyword));

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        query = query.OrderByDescending(x => x.CreatedDate);
        if (includePaging)
        {
            var pageIndex = Math.Max(1, request.PageIndex);
            var pageSize = Math.Clamp(request.PageSize, 1, 200);
            query = query.Skip((pageIndex - 1) * pageSize).Take(pageSize);
        }

        var records = await query.Select(x => new CallNotificationRecordDto
        {
            Id = x.Id, CreatedDate = x.CreatedDate, CompanyId = x.CompanyId, StoreId = x.StoreId,
            ScenarioKey = x.ScenarioKey, CustomerNumber = x.CustomerNumber, Channel = x.Channel,
            Status = x.Status, Content = x.Content, FailureReason = x.FailureReason, RetryCount = x.RetryCount
        }).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (count, records);
    }

    public Task AddAsync(CallNotificationRecord record, CancellationToken cancellationToken) => _repository.InsertAsync(record, cancellationToken);
    public Task AddAsync(CallNotificationScenarioRule rule, CancellationToken cancellationToken) => _repository.InsertAsync(rule, cancellationToken);
    public Task AddAsync(StoreCallNotificationSetting setting, CancellationToken cancellationToken) => _repository.InsertAsync(setting, cancellationToken);
    public Task UpdateAsync(CallNotificationRecord record, CancellationToken cancellationToken) => _repository.UpdateAsync(record, cancellationToken);
    public Task UpdateAsync(CallNotificationScenarioRule rule, CancellationToken cancellationToken) => _repository.UpdateAsync(rule, cancellationToken);
    public Task UpdateAsync(StoreCallNotificationSetting setting, CancellationToken cancellationToken) => _repository.UpdateAsync(setting, cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => _unitOfWork.SaveChangesAsync(cancellationToken);
}
