using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;
public partial interface IAutoTestDataProvider
{
    Task<(List<AutoTestTaskDto>, int)> GetAutoTestTasksAsync(string keyword, int? scenarioId = null, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);
    
    Task<AutoTestTask> GetAutoTestTaskByIdAsync(int id, CancellationToken cancellationToken);
    
    Task AddAutoTestTaskAsync(AutoTestTask task, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAutoTestTaskAsync(AutoTestTask task, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteAutoTestTaskAsync(AutoTestTask task, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<(int dataItemCount, int recordDoneCount)> GetDoneTaskRecordCountAsync(int dataSetId, int taskId, CancellationToken cancellationToken);
}

public partial class AutoTestDataProvider
{
    public async Task<(List<AutoTestTaskDto>, int)> GetAutoTestTasksAsync(string keyword, int? scenarioId = null, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var query =
            from task in _repository.QueryNoTracking<AutoTestTask>()
            join dataSetItem in _repository.QueryNoTracking<AutoTestDataSetItem>() 
                on task.DataSetId equals dataSetItem.DataSetId into dataSetItems
            from dataSetItem in dataSetItems.DefaultIfEmpty()
            join dataSet in _repository.QueryNoTracking<AutoTestDataSet>() 
                on dataSetItem.DataSetId equals dataSet.Id into dataSets
            from dataSet in dataSets.DefaultIfEmpty()
            join record in _repository.QueryNoTracking<AutoTestTaskRecord>().Where(x => x.Status == AutoTestTaskRecordStatus.Done)
                on task.Id equals record.TestTaskId into taskRecords
                from record in taskRecords.DefaultIfEmpty()
                group new { task, dataSet, dataSetItem, record } by task.Id into taskGroup
            select new AutoTestTaskDto
            {
                Id = taskGroup.Key,
                ScenarioId = taskGroup.Select(x => x.task.ScenarioId).FirstOrDefault(),
                DataSetId = taskGroup.Select(x => x.task.DataSetId).FirstOrDefault(),
                Params = taskGroup.Select(x => x.task.Params).FirstOrDefault(),
                Status = taskGroup.Select(x => x.task.Status).FirstOrDefault(),
                CreatedAt = taskGroup.Select(x => x.task.CreatedAt).FirstOrDefault(),
                TotalCount = taskGroup.Count(x => x.dataSetItem != null),
                InProgressCount = taskGroup.Count(x => x.record != null),
                DataSetName =  taskGroup.Select(x => x.dataSet.Name).FirstOrDefault(),
            };
        
        if (scenarioId.HasValue)
            query = query.Where(x => x.ScenarioId == scenarioId.Value);
        
        var paramList = query.Select(x => new {x.Id, ParamsDto = JsonConvert.DeserializeObject<AutoTestTaskParamsDto>(x.Params)}).ToList();

        var agentIds = paramList.Select(x => x.ParamsDto.AgentId).ToList();
        var assistantIds = paramList.Select(x => x.ParamsDto.AssistantId).ToList();
        
        var agents = await _agentDataProvider.GetAgentsByIdsAsync(agentIds, cancellationToken).ConfigureAwait(false);
        var assistants = await _agentDataProvider.GetAiSpeechAssistantsByIdsAsync(assistantIds, cancellationToken).ConfigureAwait(false);
        
        var filteredTaskIds = paramList
            .Where(x => 
            {
                if (keyword is null || string.IsNullOrEmpty(keyword)) return true;
                
                var agentName = agents.FirstOrDefault(a => a.Id == x.ParamsDto.AgentId)?.Name ?? "";
                var assistantName = assistants.FirstOrDefault(asst => asst.Id == x.ParamsDto.AssistantId)?.Name ?? "";
                return agentName.Contains(keyword) || assistantName.Contains(keyword);
            })
            .Select(x => x.Id)
            .ToList();
        
        query = query.Where(x => filteredTaskIds.Contains(x.Id));
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var dto in result)
        {
            var paramsDto = JsonConvert.DeserializeObject<AutoTestTaskParamsDto>(dto.Params);
            paramsDto.AgentName = agents.FirstOrDefault(a => a.Id == paramsDto.AgentId)?.Name ?? "";
            paramsDto.AssistantName = assistants.FirstOrDefault(asst => asst.Id == paramsDto.AssistantId)?.Name ?? "";
            dto.Params = JsonConvert.SerializeObject(paramsDto);
        }
        
        return (result, count);
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