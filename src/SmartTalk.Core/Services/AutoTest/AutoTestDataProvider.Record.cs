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
}