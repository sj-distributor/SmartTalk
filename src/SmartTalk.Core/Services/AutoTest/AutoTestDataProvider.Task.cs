using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;
public partial interface IAutoTestDataProvider
{
    Task<(List<AutoTestTaskDto>, int)> GetAutoTestTasksAsync(string keyword, int? scenarioId, int? pageIndex, int? pageSize, CancellationToken cancellationToken = default);
    
    Task<AutoTestTask> GetAutoTestTaskByIdAsync(int id, CancellationToken cancellationToken);
    
    Task AddAutoTestTaskAsync(AutoTestTask task, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAutoTestTaskAsync(AutoTestTask task, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteAutoTestTaskAsync(AutoTestTask task, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<(int dataItemCount, int recordDoneCount)> GetDoneTaskRecordCountAsync(int dataSetId, int taskId, CancellationToken cancellationToken);
}

public partial class AutoTestDataProvider
{
    public async Task<(List<AutoTestTaskDto>, int)> GetAutoTestTasksAsync(string keyword, int? scenarioId, int? pageIndex, int? pageSize, CancellationToken cancellationToken = default)
    {
        var query =
            from task in _repository.QueryNoTracking<AutoTestTask>()
            join dataSetItem in _repository.QueryNoTracking<AutoTestDataSetItem>() 
                on task.DataSetId equals dataSetItem.DataSetId into dataSetItems
            from dataSetItem in dataSetItems.DefaultIfEmpty()
            join dataSet in _repository.QueryNoTracking<AutoTestDataSet>() 
                on task.DataSetId equals dataSet.Id into dataSets
            from dataSet in dataSets.DefaultIfEmpty()
                group new { task, dataSet, dataSetItem } by task.Id into taskGroup
            select new AutoTestTaskDto
            {
                Id = taskGroup.Key,
                ScenarioId = taskGroup.Select(x => x.task.ScenarioId).FirstOrDefault(),
                DataSetId = taskGroup.Select(x => x.task.DataSetId).FirstOrDefault(),
                Params = taskGroup.Select(x => x.task.Params).FirstOrDefault(),
                Status = taskGroup.Select(x => x.task.Status).FirstOrDefault(),
                CreatedAt = taskGroup.Select(x => x.task.CreatedAt).FirstOrDefault(),
                TotalCount = taskGroup.Count(x => x.dataSetItem != null),
                DataSetName =  taskGroup.Select(x => x.dataSet.Name).FirstOrDefault(),
            };

        if (scenarioId.HasValue)
            query = query.Where(x => x.ScenarioId == scenarioId.Value);
        
        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        
        var paramDict = result.Select(x => new {x.Id, ParamsDto = JsonConvert.DeserializeObject<AutoTestTaskParamsDto>(x.Params)}).ToDictionary(x => x.Id);
        
        var agents = await _agentDataProvider.GetAgentsByIdsAsync(
            paramDict.Select(x => x.Value.ParamsDto.AgentId).ToList(),
            cancellationToken).ConfigureAwait(false);
        
        var assistants = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdsAsync(
            paramDict.Select(x => x.Value.ParamsDto.AssistantId).ToList(),
            cancellationToken).ConfigureAwait(false);
       
        var taskRecordCounts = await _repository.QueryNoTracking<AutoTestTaskRecord>().Where(r => r.Status == AutoTestTaskRecordStatus.Done).GroupBy(r => r.TestTaskId).Select(g => new { TaskId = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.TaskId, g => g.Count, cancellationToken).ConfigureAwait(false);
        
        var autoTestTasks = result.Where(x =>
        {
            x.InProgressCount = taskRecordCounts.TryGetValue(x.Id, out var inProgressCount) ? inProgressCount : 0;
            
            if (!paramDict.TryGetValue(x.Id, out var task)) return false; 
           
            task.ParamsDto.AgentName = agents.FirstOrDefault(a => a.Id == task.ParamsDto.AgentId)?.Name ?? "";
            task.ParamsDto.AssistantName = assistants.FirstOrDefault(asst => asst.Id == task.ParamsDto.AssistantId)?.Name ?? "";
            x.Params = JsonConvert.SerializeObject(task.ParamsDto);
            
            if (keyword is null || string.IsNullOrEmpty(keyword)) return true;
            
            return task.ParamsDto.AgentName.Contains(keyword, StringComparison.OrdinalIgnoreCase) || task.ParamsDto.AssistantName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }).ToList();
        
        var count = autoTestTasks.Count;
        
        if (pageIndex.HasValue && pageSize.HasValue)
            autoTestTasks = autoTestTasks.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value).ToList();
        
        return (autoTestTasks, count);
    }

    public async Task<AutoTestTask> GetAutoTestTaskByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.Query<AutoTestTask>().Where(x => x.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
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
}