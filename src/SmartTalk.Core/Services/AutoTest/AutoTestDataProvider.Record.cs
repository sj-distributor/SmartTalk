using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestDataProvider
{
    Task AddAutoTestTaskRecordsAsync(List<AutoTestTaskRecord> records, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAutoTestTaskRecordAsync(AutoTestTaskRecord record, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<AutoTestTaskRecord> GetTestTaskRecordsByIdAsync(int id, CancellationToken cancellationToken);
    
    Task<List<AutoTestTaskRecord>> GetPendingTaskRecordsByTaskIdAsync(int taskId, CancellationToken cancellationToken);
    
    Task UpdateTaskRecordsAsync(List<AutoTestTaskRecord> records, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<AutoTestTaskRecord> GetAutoTestTaskRecordBySpeechMaticsJobIdAsync(string speechMaticsJobId, CancellationToken cancellationToken = default);
    
    Task<(int Count, List<AutoTestTaskRecord> Records)> GetAutoTestTaskRecordsAsync(int taskId, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);
    
    Task<(AutoTestTask Task, AutoTestDataSet DataSet)> GetAutoTestTaskInfoByIdAsync(int taskId, CancellationToken cancellationToken);
}

public partial class AutoTestDataProvider
{
    public async Task AddAutoTestTaskRecordsAsync(List<AutoTestTaskRecord> records, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(records, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAutoTestTaskRecordAsync(AutoTestTaskRecord record, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AutoTestTaskRecord> GetTestTaskRecordsByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync<AutoTestTaskRecord>(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AutoTestTaskRecord>> GetPendingTaskRecordsByTaskIdAsync(int taskId, CancellationToken cancellationToken)
    {
        return await _repository.Query<AutoTestTaskRecord>().Where(x => x.TestTaskId == taskId && x.Status == AutoTestTaskRecordStatus.Pending).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<AutoTestTaskRecord> GetAutoTestTaskRecordBySpeechMaticsJobIdAsync(string speechMaticsJobId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<AutoTestTaskRecord>()
            .Where(x => x.SpeechMaticsJobId == speechMaticsJobId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task UpdateTaskRecordsAsync(List<AutoTestTaskRecord> records, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(records, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<(int Count, List<AutoTestTaskRecord> Records)> GetAutoTestTaskRecordsAsync(
        int taskId, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<AutoTestTaskRecord>(x => x.Id == taskId);
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.OrderBy(x => x.IsArchived).ThenBy(x => x.DataSetItemId).Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        var records = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return (count, records);
    }

    public async Task<(AutoTestTask Task, AutoTestDataSet DataSet)> GetAutoTestTaskInfoByIdAsync(int taskId, CancellationToken cancellationToken)
    {
        var query = from dataset in _repository.QueryNoTracking<AutoTestDataSet>()
            join task in _repository.QueryNoTracking<AutoTestTask>().Where(x => x.Id == taskId) on dataset.Id equals task.DataSetId
            select new { task, dataset };
        
        var result = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return (result?.task, result?.dataset);
    }
}