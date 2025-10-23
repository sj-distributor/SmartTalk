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
    
    Task<(List<AutoTestTestTaskDto>, int)> GetAutoTestTestTasksAsync(string keyword, int? scenarioId = null, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);
    
    Task<AutoTestTestTask> GetAutoTestTestTaskByIdAsync(int id, CancellationToken cancellationToken);
    
    Task AddAutoTestTestTaskAsync(AutoTestTestTask testTask, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAutoTestTestTaskAsync(AutoTestTestTask testTask, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteAutoTestTestTaskAsync(AutoTestTestTask testTask, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<AutoTestDataItem>> GetAutoTestDataItemsBySetIdAsync(int testDataSetId, CancellationToken cancellationToken);

    Task AddAutoTestTestTaskRecordsAsync(List<AutoTestTaskRecord> records, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAutoTestTestTaskRecordAsync(AutoTestTaskRecord record, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<AutoTestTaskRecord> GetTestTaskRecordsByIdAsync(int id, CancellationToken cancellationToken);
    
    Task<List<AutoTestTaskRecord>> GetPendingTestTaskRecordsByTaskIdAsync(int taskId, CancellationToken cancellationToken);

    Task<(int dataItemCount, int testRecordDoneCount)> GetDoneTestTaskRecordCountAsync(int testDataSetId, int taskId, CancellationToken cancellationToken);
    
    Task UpdateTestTaskRecordsAsync(List<AutoTestTaskRecord> records, bool forceSave = true, CancellationToken cancellationToken = default);
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

    public async Task<(List<AutoTestTestTaskDto>, int)> GetAutoTestTestTasksAsync(string keyword, int? scenarioId = null, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var query =
            from task in _repository.QueryNoTracking<AutoTestTestTask>()
            join dataSetItem in _repository.QueryNoTracking<AutoTestDataSetItem>()
                on task.DataSetId equals dataSetItem.DataSetId into dataSetItems
            join record in _repository.QueryNoTracking<AutoTestTaskRecord>().Where(x => x.Status == AutoTestTestTaskRecordStatus.Done)
                on task.Id equals record.TestTaskId into taskRecords
            select new AutoTestTestTaskDto
            {
                Id = task.Id,
                ScenarioId = task.ScenarioId,
                DataSetId = task.DataSetId,
                Params = task.Params,
                Status = task.Status,
                CreatedAt = task.CreatedAt,
                TotalCount = dataSetItems.Count(),
                InProgressCount = taskRecords.Count()
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

    public async Task<AutoTestTestTask> GetAutoTestTestTaskByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync<AutoTestTestTask>(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAutoTestTestTaskAsync(AutoTestTestTask testTask, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(testTask, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAutoTestTestTaskAsync(AutoTestTestTask testTask, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(testTask, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAutoTestTestTaskAsync(AutoTestTestTask testTask, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(testTask, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AutoTestDataItem>> GetAutoTestDataItemsBySetIdAsync(int testDataSetId, CancellationToken cancellationToken)
    {
        return await (from testDataSetItem in _repository.Query<AutoTestDataSetItem>().Where(x => x.DataSetId == testDataSetId)
            join testDataItem in _repository.Query<AutoTestDataItem>() on testDataSetItem.DataItemId equals testDataItem.Id 
            select testDataItem).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAutoTestTestTaskRecordsAsync(List<AutoTestTaskRecord> records, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(records, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAutoTestTestTaskRecordAsync(AutoTestTaskRecord record, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AutoTestTaskRecord> GetTestTaskRecordsByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync<AutoTestTaskRecord>(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AutoTestTaskRecord>> GetPendingTestTaskRecordsByTaskIdAsync(int taskId, CancellationToken cancellationToken)
    {
        return await _repository.Query<AutoTestTaskRecord>().Where(x => x.TestTaskId == taskId && x.Status == AutoTestTestTaskRecordStatus.Pending).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<(int dataItemCount, int testRecordDoneCount)> GetDoneTestTaskRecordCountAsync(int testDataSetId, int taskId, CancellationToken cancellationToken)
    {
        var dataItemCount =
            await (from testDataSetItem in _repository.Query<AutoTestDataSetItem>()
                    .Where(x => x.DataSetId == testDataSetId)
                join testDataItem in _repository.Query<AutoTestDataItem>() on testDataSetItem.DataItemId equals
                    testDataItem.Id
                select testDataItem).CountAsync(cancellationToken).ConfigureAwait(false);
        
        var testRecordDoneCount = await _repository.Query<AutoTestTaskRecord>().Where(x => x.TestTaskId == taskId && x.Status == AutoTestTestTaskRecordStatus.Done).CountAsync(cancellationToken).ConfigureAwait(false);
        
        return (dataItemCount, testRecordDoneCount);
    }

    public async Task UpdateTestTaskRecordsAsync(List<AutoTestTaskRecord> records, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(records, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}