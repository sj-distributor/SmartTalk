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
    
    Task AddAutoTestTestTaskAsync(AutoTestTestTask testTask, CancellationToken cancellationToken);
    
    Task UpdateAutoTestTestTaskAsync(AutoTestTestTask testTask, CancellationToken cancellationToken);

    Task DeleteAutoTestTestTaskAsync(AutoTestTestTask testTask, CancellationToken cancellationToken);

    Task<List<AutoTestDataItem>> GetAutoTestDataItemsBySetIdAsync(int testDataSetId, CancellationToken cancellationToken);

    Task AddAutoTestTestTaskRecordsAsync(List<AutoTestTestTaskRecord> records, CancellationToken cancellationToken);
    
    Task UpdateAutoTestTestTaskRecordAsync(AutoTestTestTaskRecord record, CancellationToken cancellationToken);
    
    Task<AutoTestTestTaskRecord> GetTestTaskRecordsByIdAsync(int id, CancellationToken cancellationToken);
    
    Task<List<AutoTestTestTaskRecord>> GetPendingTestTaskRecordsByTaskIdAsync(int taskId, CancellationToken cancellationToken);

    Task<(int dataItemCount, int testRecordDoneCount)> GetDoneTestTaskRecordCountAsync(int testDataSetId, int taskId, CancellationToken cancellationToken);
    
    Task UpdateTestTaskRecordsAsync(List<AutoTestTestTaskRecord> records, CancellationToken cancellationToken);
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
        var query = _repository.Query<AutoTestTestTask>();
        
        if (scenarioId.HasValue)
            query = query.Where(x => x.ScenarioId == scenarioId.Value);
        
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Params.Contains(keyword));
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
       
        var tasks = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
         
         var taskIds = tasks.Select(t => t.Id).ToList();
         var dataSetIds = tasks.Select(t => t.DataSetId).Distinct().ToList();
         
         var dataItemCounts = await _repository.QueryNoTracking<AutoTestDataSetItem>()
             .Where(x => dataSetIds.Contains(x.DataSetId))
             .GroupBy(x => x.DataSetId)
             .Select(g => new { g.Key, Count = g.Count() })
             .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
         
         var doneCounts = await _repository.QueryNoTracking<AutoTestTestTaskRecord>()
             .Where(x => taskIds.Contains(x.TestTaskId) && x.Status == AutoTestTestTaskRecordStatus.Done)
             .GroupBy(x => x.TestTaskId)
             .Select(g => new { g.Key, Count = g.Count() })
             .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

         var result = tasks.Select(t => new AutoTestTestTaskDto
         {
             Id = t.Id,
             ScenarioId = t.ScenarioId,
             DataSetId = t.DataSetId,
             Params = t.Params,
             Status = t.Status,
             CreatedAt = t.CreatedAt,
             TotalCount = dataItemCounts.TryGetValue(t.DataSetId, out var totalCount) ? totalCount : 0,
             InProgressCount = doneCounts.TryGetValue(t.Id, out var doneCount) ? doneCount : 0
         }).ToList();
        
        return (result, count);
    }

    public async Task<AutoTestTestTask> GetAutoTestTestTaskByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync<AutoTestTestTask>(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAutoTestTestTaskAsync(AutoTestTestTask testTask, CancellationToken cancellationToken)
    {
        await _repository.InsertAsync(testTask, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAutoTestTestTaskAsync(AutoTestTestTask testTask, CancellationToken cancellationToken)
    {
        await _repository.UpdateAsync(testTask, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAutoTestTestTaskAsync(AutoTestTestTask testTask, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(testTask, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AutoTestDataItem>> GetAutoTestDataItemsBySetIdAsync(int testDataSetId, CancellationToken cancellationToken)
    {
        return await (from testDataSetItem in _repository.Query<AutoTestDataSetItem>().Where(x => x.DataSetId == testDataSetId)
            join testDataItem in _repository.Query<AutoTestDataItem>() on testDataSetItem.DataItemId equals testDataItem.Id 
            select testDataItem).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAutoTestTestTaskRecordsAsync(List<AutoTestTestTaskRecord> records, CancellationToken cancellationToken)
    {
        await _repository.InsertAllAsync(records, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAutoTestTestTaskRecordAsync(AutoTestTestTaskRecord record, CancellationToken cancellationToken)
    {
        await _repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AutoTestTestTaskRecord> GetTestTaskRecordsByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync<AutoTestTestTaskRecord>(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AutoTestTestTaskRecord>> GetPendingTestTaskRecordsByTaskIdAsync(int taskId, CancellationToken cancellationToken)
    {
        return await _repository.Query<AutoTestTestTaskRecord>().Where(x => x.TestTaskId == taskId && x.Status == AutoTestTestTaskRecordStatus.Pending).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<(int dataItemCount, int testRecordDoneCount)> GetDoneTestTaskRecordCountAsync(int testDataSetId, int taskId, CancellationToken cancellationToken)
    {
        var dataItemCount =
            await (from testDataSetItem in _repository.Query<AutoTestDataSetItem>()
                    .Where(x => x.DataSetId == testDataSetId)
                join testDataItem in _repository.Query<AutoTestDataItem>() on testDataSetItem.DataItemId equals
                    testDataItem.Id
                select testDataItem).CountAsync(cancellationToken).ConfigureAwait(false);
        
        var testRecordDoneCount = await _repository.Query<AutoTestTestTaskRecord>().Where(x => x.TestTaskId == taskId && x.Status == AutoTestTestTaskRecordStatus.Done).CountAsync(cancellationToken).ConfigureAwait(false);
        
        return (dataItemCount, testRecordDoneCount);
    }

    public async Task UpdateTestTaskRecordsAsync(List<AutoTestTestTaskRecord> records, CancellationToken cancellationToken)
    {
        await _repository.UpdateAllAsync(records, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}