using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestService
{
    Task<GetAutoTestTestTaskResponse> GetAutoTestTestTasksAsync(GetAutoTestTestTaskRequest request, CancellationToken cancellationToken);
    
    Task<CreateAutoTestTestTaskResponse> CreateAutoTestTestTaskAsync(CreateAutoTestTestTaskCommand command, CancellationToken cancellationToken);
    
    Task<UpdateAutoTestTestTaskResponse> UpdateAutoTestTestTaskAsync(UpdateAutoTestTestTaskCommand command, CancellationToken cancellationToken);
    
    Task<DeleteAutoTestTestTaskResponse> DeleteAutoTestTestTaskAsync(DeleteAutoTestTestTaskCommand command, CancellationToken cancellationToken);
}

public partial class AutoTestService
{
    public async Task<GetAutoTestTestTaskResponse> GetAutoTestTestTasksAsync(GetAutoTestTestTaskRequest request, CancellationToken cancellationToken)
    {
        var (testTasks, count) = await _autoTestDataProvider.GetAutoTestTestTasksAsync(request.KeyWord, request.ScenarioId, request.PageIndex, request.PageSize, cancellationToken).ConfigureAwait(false);
        
        return new GetAutoTestTestTaskResponse
        {
            Data = testTasks,
            TotalCount = count
        };
    }

    public async Task<CreateAutoTestTestTaskResponse> CreateAutoTestTestTaskAsync(CreateAutoTestTestTaskCommand command, CancellationToken cancellationToken)
    {
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(command.TestTask.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        if (scenario == null) throw new Exception("Scenario not found");

        var testTask = _mapper.Map<AutoTestTestTask>(command.TestTask);
        
        await _autoTestDataProvider.AddAutoTestTestTaskAsync(testTask, cancellationToken).ConfigureAwait(false);

        var dataItems = await _autoTestDataProvider.GetAutoTestDataItemsBySetIdAsync(testTask.DataSetId, cancellationToken).ConfigureAwait(false);

        var testTaskRecords = dataItems.Select(x => new AutoTestTaskRecord
        {
            TestTaskId = testTask.Id,
            ScenarioId = testTask.ScenarioId,
            DataSetId = testTask.DataSetId,
            DataSetItemId = x.Id,
            InputSnapshot = x.InputJson,
            RequestJson = testTask.Params,
            Status = AutoTestTestTaskRecordStatus.Pending,
            CreatedAt = DateTimeOffset.Now
        }).ToList();
                    
        await _autoTestDataProvider.AddAutoTestTestTaskRecordsAsync(testTaskRecords, cancellationToken).ConfigureAwait(false);

        var task = _mapper.Map<AutoTestTestTaskDto>(testTask);
        task.TotalCount = testTaskRecords.Count;
        task.InProgressCount = 0;
        
        return new CreateAutoTestTestTaskResponse
        {
            Data = task
        };
    }

    public async Task<UpdateAutoTestTestTaskResponse> UpdateAutoTestTestTaskAsync(UpdateAutoTestTestTaskCommand command, CancellationToken cancellationToken)
    {
        var testTask = await _autoTestDataProvider.GetAutoTestTestTaskByIdAsync(command.TestTaskId, cancellationToken).ConfigureAwait(false);
        
        if (testTask == null) throw new Exception("UpdateAutoTestTestTaskAsync Test task not found");

        var (dataItemCount, testRecordDoneCount) = await HandleStatusChangeAsync(testTask, command.Status, cancellationToken).ConfigureAwait(false);

        var task = _mapper.Map<AutoTestTestTaskDto>(testTask);
        task.TotalCount = dataItemCount;
        task.InProgressCount = testRecordDoneCount;
        
        return new UpdateAutoTestTestTaskResponse
        {
            Data = task
        };
    }

    private async Task<(int dataItemCount, int testRecordDoneCount)> HandleStatusChangeAsync(AutoTestTestTask testTask, AutoTestTestTaskStatus newStatus, CancellationToken cancellationToken)
    {
        testTask.Status = newStatus;
        
        testTask.StartedAt ??= DateTimeOffset.Now; 
        
        await _autoTestDataProvider.UpdateAutoTestTestTaskAsync(testTask, cancellationToken).ConfigureAwait(false); 
        
        switch (newStatus)
        {
            case AutoTestTestTaskStatus.Pause:
                await UpdateTestTaskRecordsStatusAsync(testTask.Id, AutoTestTestTaskRecordStatus.Pause, cancellationToken).ConfigureAwait(false);
                break;

            case AutoTestTestTaskStatus.Ongoing:
                if (testTask.StartedAt is not null) await UpdateTestTaskRecordsStatusAsync(testTask.Id, AutoTestTestTaskRecordStatus.Pending, cancellationToken).ConfigureAwait(false);
                await AutoTestRunningAsync(new AutoTestRunningCommand
                {
                    TaskId = testTask.Id,
                    ScenarioId = testTask.ScenarioId,
                }, cancellationToken).ConfigureAwait(false);
                break;
        }
        
        var (dataItemCount, testRecordDoneCount) = await _autoTestDataProvider.GetDoneTestTaskRecordCountAsync(testTask.DataSetId, testTask.Id, cancellationToken).ConfigureAwait(false);

        if (dataItemCount == testRecordDoneCount)
        {
            testTask.FinishedAt = DateTimeOffset.Now; 
            testTask.Status = AutoTestTestTaskStatus.Done;
            await _autoTestDataProvider.UpdateAutoTestTestTaskAsync(testTask, cancellationToken).ConfigureAwait(false); 
        }
        
        return (dataItemCount, testRecordDoneCount);
    }

    private async Task UpdateTestTaskRecordsStatusAsync(int testTaskId, AutoTestTestTaskRecordStatus status, CancellationToken cancellationToken)
    {
        var taskRecords = await _autoTestDataProvider.GetPendingTestTaskRecordsByTaskIdAsync(testTaskId, cancellationToken).ConfigureAwait(false);

        taskRecords.ForEach(x => x.Status = status);
        
        await _autoTestDataProvider.UpdateTestTaskRecordsAsync(taskRecords, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<DeleteAutoTestTestTaskResponse> DeleteAutoTestTestTaskAsync(DeleteAutoTestTestTaskCommand command, CancellationToken cancellationToken)
    {
        var testTask = await _autoTestDataProvider.GetAutoTestTestTaskByIdAsync(command.TestTaskId, cancellationToken).ConfigureAwait(false);
        
        if (testTask != null) await _autoTestDataProvider.DeleteAutoTestTestTaskAsync(testTask, cancellationToken).ConfigureAwait(false);
        
        return new DeleteAutoTestTestTaskResponse();
    }
}