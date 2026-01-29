using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestService
{
    Task<GetAutoTestTaskResponse> GetAutoTestTasksAsync(GetAutoTestTaskRequest request, CancellationToken cancellationToken);
    
    Task<CreateAutoTestTaskResponse> CreateAutoTestTaskAsync(CreateAutoTestTaskCommand command, CancellationToken cancellationToken);
    
    Task<UpdateAutoTestTaskResponse> UpdateAutoTestTaskAsync(UpdateAutoTestTaskCommand command, CancellationToken cancellationToken);
    
    Task<DeleteAutoTestTaskResponse> DeleteAutoTestTaskAsync(DeleteAutoTestTaskCommand command, CancellationToken cancellationToken);
    
    Task<GetAutoTestTaskRecordsResponse> GetAutoTestTaskRecordsAsync(GetAutoTestTaskRecordsRequest request, CancellationToken cancellationToken);
    
    Task<MarkAutoTestTaskRecordResponse> MarkAutoTestTaskRecordAsync(MarkAutoTestTaskRecordCommand command, CancellationToken cancellationToken);
}

public partial class AutoTestService
{
    public async Task<GetAutoTestTaskResponse> GetAutoTestTasksAsync(GetAutoTestTaskRequest request, CancellationToken cancellationToken)
    {
        var (tasks, count) = await _autoTestDataProvider.GetAutoTestTasksAsync(request.KeyWord, request.ScenarioId, request.PageIndex, request.PageSize, cancellationToken).ConfigureAwait(false);
        
        return new GetAutoTestTaskResponse
        {
            Data = new GetAutoTestTaskResponseDataDto()
            {
                TotalCount = count,
                Tasks = tasks
            }
        };
    }

    public async Task<CreateAutoTestTaskResponse> CreateAutoTestTaskAsync(CreateAutoTestTaskCommand command, CancellationToken cancellationToken)
    {
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(command.Task.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        if (scenario == null) throw new Exception("Scenario not found");

        var task = _mapper.Map<AutoTestTask>(command.Task);
        
        var paramInfos  = JsonConvert.DeserializeObject<AutoTestTaskParamsDto>(task.Params);
        
        var agents = await _agentDataProvider.GetAgentByIdAsync(paramInfos.AgentId, cancellationToken).ConfigureAwait(false);
        
        if (agents == null) throw new Exception("Agent not found");
        
        var assistants = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantAsync(paramInfos.AssistantId, cancellationToken).ConfigureAwait(false);
        
        if (assistants == null) throw new Exception("assistants not found");
        
        await _autoTestDataProvider.AddAutoTestTaskAsync(task, cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.Information("CreateAutoTestTaskAsync task :{@task}", task);
        
        var dataItems = await _autoTestDataProvider.GetAutoTestDataItemsBySetIdAsync(task.DataSetId, cancellationToken).ConfigureAwait(false);

        Log.Information("CreateAutoTestTaskAsync dataItems :{@dataItems}", dataItems);
        
        var records = dataItems.OrderBy(x => x.CreatedAt).Select((x, index) => new AutoTestTaskRecord
        {
            TestTaskId = task.Id,
            ScenarioId = task.ScenarioId,
            DataSetId = task.DataSetId,
            DataSetItemId = x.Id,
            InputSnapshot = x.InputJson,
            RequestJson = task.Params,
            Status = AutoTestTaskRecordStatus.Pending,
            CreatedAt = DateTimeOffset.Now.AddMilliseconds(50 + index)
        }).ToList();
                    
        Log.Information("CreateAutoTestTaskAsync records :{@records}", records);
        
        await _autoTestDataProvider.AddAutoTestTaskRecordsAsync(records, cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.Information("CreateAutoTestTaskAsync records after :{@records}", records);
        
        var result = _mapper.Map<AutoTestTaskDto>(task);
        result.TotalCount = records.Count;
        result.InProgressCount = 0;
        
        return new CreateAutoTestTaskResponse
        {
            Data = result
        };
    }

    public async Task<UpdateAutoTestTaskResponse> UpdateAutoTestTaskAsync(UpdateAutoTestTaskCommand command, CancellationToken cancellationToken)
    {
        var task = await _autoTestDataProvider.GetAutoTestTaskByIdAsync(command.TaskId, cancellationToken).ConfigureAwait(false);
        
        if (task == null) throw new Exception("UpdateAutoTestTaskAsync Test task not found");

        var (dataItemCount, recordDoneCount) = await HandleStatusChangeAsync(task, command.Status, cancellationToken).ConfigureAwait(false);

        var result = _mapper.Map<AutoTestTaskDto>(task);
        result.TotalCount = dataItemCount;
        result.InProgressCount = recordDoneCount;
        
        return new UpdateAutoTestTaskResponse
        {
            Data = result
        };
    }

    private async Task<(int dataItemCount, int recordDoneCount)> HandleStatusChangeAsync(AutoTestTask task, AutoTestTaskStatus newStatus, CancellationToken cancellationToken)
    {
        task.Status = newStatus;
        
        task.StartedAt ??= DateTimeOffset.Now; 
        
        await _autoTestDataProvider.UpdateAutoTestTaskAsync(task, cancellationToken: cancellationToken).ConfigureAwait(false); 
        
        Log.Information("HandleStatusChangeAsync task :{@task}", task);
        
        switch (newStatus)
        {
            case AutoTestTaskStatus.Pause:
                await UpdateTaskRecordsStatusAsync(task.Id, AutoTestTaskRecordStatus.Ongoing,AutoTestTaskRecordStatus.Pause,  cancellationToken).ConfigureAwait(false);
                break;

            case AutoTestTaskStatus.Ongoing:
                if (task.StartedAt is not null) await UpdateTaskRecordsStatusAsync(task.Id, AutoTestTaskRecordStatus.Pause, AutoTestTaskRecordStatus.Ongoing, cancellationToken).ConfigureAwait(false);
                await AutoTestRunningAsync(new AutoTestRunningCommand
                {
                    TaskId = task.Id,
                    ScenarioId = task.ScenarioId,
                }, cancellationToken).ConfigureAwait(false);
                break;
        }
        
        var (dataItemCount, recordDoneCount) = await _autoTestDataProvider.GetDoneTaskRecordCountAsync(task.DataSetId, task.Id, cancellationToken).ConfigureAwait(false);
        
        return (dataItemCount, recordDoneCount);
    }

    private async Task UpdateTaskRecordsStatusAsync(int testTaskId, AutoTestTaskRecordStatus status, AutoTestTaskRecordStatus updateStatus, CancellationToken cancellationToken)
    {
        var records = await _autoTestDataProvider.GetStatusTaskRecordsByTaskIdAsync(testTaskId, status, cancellationToken).ConfigureAwait(false);

        if (records.Any())
        {
            records.ForEach(x => x.Status = updateStatus);
        
            await _autoTestDataProvider.UpdateTaskRecordsAsync(records, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        
        Log.Information("UpdateTaskRecordsStatusAsync records :{@records}", records);
    }
    
    public async Task<DeleteAutoTestTaskResponse> DeleteAutoTestTaskAsync(DeleteAutoTestTaskCommand command, CancellationToken cancellationToken)
    {
        var testTask = await _autoTestDataProvider.GetAutoTestTaskByIdAsync(command.TaskId, cancellationToken).ConfigureAwait(false);
        
        if (testTask != null) await _autoTestDataProvider.DeleteAutoTestTaskAsync(testTask, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new DeleteAutoTestTaskResponse();
    }
    
    public async Task<GetAutoTestTaskRecordsResponse> GetAutoTestTaskRecordsAsync(GetAutoTestTaskRecordsRequest request, CancellationToken cancellationToken)
    {
        var (count, records) = await _autoTestDataProvider.GetAutoTestTaskRecordsAsync(request.TaskId, request.PageIndex, request.PageSize, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var taskInfo = await BuildAutoTestTaskInfoAsync(request.TaskId, cancellationToken).ConfigureAwait(false);
        
        return new GetAutoTestTaskRecordsResponse
        {
            Data = new GetAutoTestTaskRecordsResponseData
            {
                Count = count,
                TaskInfo = _mapper.Map<AutoTestTaskInfoDto>(taskInfo),
                TaskRecords = _mapper.Map<List<AutoTestTaskRecordDto>>(records)
            }
        };
    }
    
    public async Task<MarkAutoTestTaskRecordResponse> MarkAutoTestTaskRecordAsync(MarkAutoTestTaskRecordCommand command, CancellationToken cancellationToken)
    {
        var record = await _autoTestDataProvider.GetTestTaskRecordsByIdAsync(command.RecordId, cancellationToken).ConfigureAwait(false);

        if (record == null) throw new Exception("MarkAutoTestTaskRecordAsync Test task not found");
        
        record.IsArchived = command.IsArchived;
        
        await _autoTestDataProvider.UpdateAutoTestTaskRecordAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new MarkAutoTestTaskRecordResponse { Data = _mapper.Map<AutoTestTaskRecordDto>(record) };
    }
    
    private async Task<AutoTestTaskInfoDto> BuildAutoTestTaskInfoAsync(int taskId, CancellationToken cancellationToken)
    {
        var (task, dataset) = await _autoTestDataProvider.GetAutoTestTaskInfoByIdAsync(taskId, cancellationToken).ConfigureAwait(false);
        
        var taskParams = JsonConvert.DeserializeObject<AutoTestTaskParamsDto>(task.Params);
        
        Log.Information("Get task params: {AssistantId}", taskParams);
        
        var assistant = taskParams != null && taskParams.AssistantId != 0 ? await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(taskParams.AssistantId, cancellationToken).ConfigureAwait(false) : null;

        Log.Information("Source assistant for executing the current task: {@Assistant}", assistant);
        
        return new AutoTestTaskInfoDto
        {
            AssistantName = assistant?.Name ?? string.Empty,
            TestDataName = dataset.Name,
            CreadtedAt = task.CreatedAt
        };
    }
}