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
}

public partial class AutoTestService
{
    public async Task<GetAutoTestTaskResponse> GetAutoTestTasksAsync(GetAutoTestTaskRequest request, CancellationToken cancellationToken)
    {
        var (tasks, count) = await _autoTestDataProvider.GetAutoTestTasksAsync(request.KeyWord, request.ScenarioId, request.PageIndex, request.PageSize, cancellationToken).ConfigureAwait(false);
        
        return new GetAutoTestTaskResponse
        {
            Data = tasks,
            TotalCount = count
        };
    }

    public async Task<CreateAutoTestTaskResponse> CreateAutoTestTaskAsync(CreateAutoTestTaskCommand command, CancellationToken cancellationToken)
    {
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(command.Task.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        if (scenario == null) throw new Exception("Scenario not found");

        var task = _mapper.Map<AutoTestTask>(command.Task);
        
        await _autoTestDataProvider.AddAutoTestTaskAsync(task, cancellationToken: cancellationToken).ConfigureAwait(false);

        var dataItems = await _autoTestDataProvider.GetAutoTestDataItemsBySetIdAsync(task.DataSetId, cancellationToken).ConfigureAwait(false);

        var records = dataItems.Select(x => new AutoTestTaskRecord
        {
            TestTaskId = task.Id,
            ScenarioId = task.ScenarioId,
            DataSetId = task.DataSetId,
            DataSetItemId = x.Id,
            InputSnapshot = x.InputJson,
            RequestJson = task.Params,
            Status = AutoTestTaskRecordStatus.Pending,
            CreatedAt = DateTimeOffset.Now
        }).ToList();
                    
        await _autoTestDataProvider.AddAutoTestTaskRecordsAsync(records, cancellationToken: cancellationToken).ConfigureAwait(false);

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
        
        switch (newStatus)
        {
            case AutoTestTaskStatus.Pause:
                await UpdateTaskRecordsStatusAsync(task.Id, AutoTestTaskRecordStatus.Pause, cancellationToken).ConfigureAwait(false);
                break;

            case AutoTestTaskStatus.Ongoing:
                if (task.StartedAt is not null) await UpdateTaskRecordsStatusAsync(task.Id, AutoTestTaskRecordStatus.Pending, cancellationToken).ConfigureAwait(false);
                await AutoTestRunningAsync(new AutoTestRunningCommand
                {
                    TaskId = task.Id,
                    ScenarioId = task.ScenarioId,
                }, cancellationToken).ConfigureAwait(false);
                break;
        }
        
        var (dataItemCount, recordDoneCount) = await _autoTestDataProvider.GetDoneTaskRecordCountAsync(task.DataSetId, task.Id, cancellationToken).ConfigureAwait(false);

        if (dataItemCount == recordDoneCount)
        {
            task.FinishedAt = DateTimeOffset.Now; 
            task.Status = AutoTestTaskStatus.Done;
            await _autoTestDataProvider.UpdateAutoTestTaskAsync(task, cancellationToken: cancellationToken).ConfigureAwait(false); 
        }
        
        return (dataItemCount, recordDoneCount);
    }

    private async Task UpdateTaskRecordsStatusAsync(int testTaskId, AutoTestTaskRecordStatus status, CancellationToken cancellationToken)
    {
        var records = await _autoTestDataProvider.GetPendingTaskRecordsByTaskIdAsync(testTaskId, cancellationToken).ConfigureAwait(false);

        records.ForEach(x => x.Status = status);
        
        await _autoTestDataProvider.UpdateTaskRecordsAsync(records, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<DeleteAutoTestTaskResponse> DeleteAutoTestTaskAsync(DeleteAutoTestTaskCommand command, CancellationToken cancellationToken)
    {
        var testTask = await _autoTestDataProvider.GetAutoTestTaskByIdAsync(command.TaskId, cancellationToken).ConfigureAwait(false);
        
        if (testTask != null) await _autoTestDataProvider.DeleteAutoTestTaskAsync(testTask, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new DeleteAutoTestTaskResponse();
    }
}