using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestDataProvider : IScopedDependency
{
    Task<AutoTestScenario> GetAutoTestScenarioByIdAsync(int id, CancellationToken cancellationToken);
    
    Task<(List<AutoTestTaskDto>, int)> GetAutoTestTasksAsync(string keyword, int? scenarioId = null, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);
    
    Task<AutoTestTask> GetAutoTestTaskByIdAsync(int id, CancellationToken cancellationToken);
    
    Task AddAutoTestTaskAsync(AutoTestTask task, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAutoTestTaskAsync(AutoTestTask task, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteAutoTestTaskAsync(AutoTestTask task, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<AutoTestDataItem>> GetAutoTestDataItemsBySetIdAsync(int dataSetId, CancellationToken cancellationToken);

    Task AddAutoTestTaskRecordsAsync(List<AutoTestTaskRecord> records, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAutoTestTaskRecordAsync(AutoTestTaskRecord record, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<AutoTestTaskRecord> GetTestTaskRecordsByIdAsync(int id, CancellationToken cancellationToken);
    
    Task<List<AutoTestTaskRecord>> GetPendingTaskRecordsByTaskIdAsync(int taskId, CancellationToken cancellationToken);

    Task<(int dataItemCount, int recordDoneCount)> GetDoneTaskRecordCountAsync(int dataSetId, int taskId, CancellationToken cancellationToken);
    
    Task UpdateTaskRecordsAsync(List<AutoTestTaskRecord> records, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class AutoTestDataProvider : IAutoTestDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    
    public AutoTestDataProvider(IRepository repository, IMapper mapper, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _unitOfWork = unitOfWork;
        _repository = repository;
    }

    public async Task<AutoTestScenario> GetAutoTestScenarioByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync<AutoTestScenario>(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(List<AutoTestTaskDto>, int)> GetAutoTestTasksAsync(string keyword, int? scenarioId = null, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var query =
            from task in _repository.QueryNoTracking<AutoTestTask>()
            join dataSetItem in _repository.QueryNoTracking<AutoTestDataSetItem>() 
                on task.DataSetId equals dataSetItem.DataSetId into dataSetItems
            from dataSetItem in dataSetItems.DefaultIfEmpty()
            join record in _repository.QueryNoTracking<AutoTestTaskRecord>().Where(x => x.Status == AutoTestTaskRecordStatus.Done)
                on task.Id equals record.TestTaskId into taskRecords
                from record in taskRecords.DefaultIfEmpty()
                group new { task, dataSetItem, record } by task.Id into taskGroup
            select new AutoTestTaskDto
            {
                Id = taskGroup.Key,
                ScenarioId = taskGroup.Select(x => x.task.ScenarioId).FirstOrDefault(),
                DataSetId = taskGroup.Select(x => x.task.DataSetId).FirstOrDefault(),
                Params = taskGroup.Select(x => x.task.Params).FirstOrDefault(),
                Status = taskGroup.Select(x => x.task.Status).FirstOrDefault(),
                CreatedAt = taskGroup.Select(x => x.task.CreatedAt).FirstOrDefault(),
                TotalCount = taskGroup.Count(x => x.dataSetItem != null),
                InProgressCount = taskGroup.Count(x => x.record != null)
            };
        
        if (scenarioId.HasValue)
            query = query.Where(x => x.ScenarioId == scenarioId.Value);
        
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Params.Contains(keyword));
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        return (await query.ToListAsync(cancellationToken).ConfigureAwait(false), count);
    }

    public async Task<AutoTestTask> GetAutoTestTaskByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync<AutoTestTask>(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAutoTestTaskAsync(AutoTestTask task, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(task, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAutoTestTaskAsync(AutoTestTask task, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAutoTestTaskAsync(AutoTestTask task, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(task, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AutoTestDataItem>> GetAutoTestDataItemsBySetIdAsync(int dataSetId, CancellationToken cancellationToken)
    {
        return await (from testDataSetItem in _repository.Query<AutoTestDataSetItem>().Where(x => x.DataSetId == dataSetId)
            join dataItem in _repository.Query<AutoTestDataItem>() on testDataSetItem.DataItemId equals dataItem.Id 
            select dataItem).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

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
    
    public async Task<(int dataItemCount, int recordDoneCount)> GetDoneTaskRecordCountAsync(int dataSetId, int taskId, CancellationToken cancellationToken)
    {
        var dataItemCount =
            await (from dataSetItem in _repository.Query<AutoTestDataSetItem>()
                    .Where(x => x.DataSetId == dataSetId)
                join dataItem in _repository.Query<AutoTestDataItem>() on dataSetItem.DataItemId equals
                    dataItem.Id
                select dataItem).CountAsync(cancellationToken).ConfigureAwait(false);
        
        var recordDoneCount = await _repository.Query<AutoTestTaskRecord>().Where(x => x.TestTaskId == taskId && x.Status == AutoTestTaskRecordStatus.Done).CountAsync(cancellationToken).ConfigureAwait(false);
        
        return (dataItemCount, recordDoneCount);
    }

    public async Task UpdateTaskRecordsAsync(List<AutoTestTaskRecord> records, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(records, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}