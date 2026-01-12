using System.Text.Json;
using Serilog;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Enums.Sales;

namespace SmartTalk.Core.Services.Sale;

public interface ISalesPhoneOrderPushService : IScopedDependency
{
    Task ExecutePhoneOrderPushTasksAsync(int recordId, CancellationToken cancellationToken);
}

public class SalesPhoneOrderPushService : ISalesPhoneOrderPushService
{
    private readonly ISalesClient _salesClient;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public SalesPhoneOrderPushService(ISalesDataProvider salesDataProvider, ISalesClient salesClient, IPhoneOrderDataProvider phoneOrderDataProvider, ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _salesClient = salesClient;
        _salesDataProvider = salesDataProvider;
        _backgroundJobClient = backgroundJobClient;
        _phoneOrderDataProvider = phoneOrderDataProvider;
    }
    
    public async Task ExecutePhoneOrderPushTasksAsync(int recordId, CancellationToken cancellationToken)
    {
        Log.Information("Start ExecutePhoneOrderPushTasksAsync for RecordId={RecordId}", recordId);
        var task = await _salesDataProvider.GetRecordPushTaskByRecordIdAsync(recordId, cancellationToken).ConfigureAwait(false);
        
        if (task == null)
        {
            Log.Information("No task found for RecordId={RecordId}", recordId);
            return;
        }
        
        var parentCompleted = await _salesDataProvider.IsParentCompletedAsync(task.ParentRecordId, cancellationToken).ConfigureAwait(false);
        Log.Information("ParentCompleted={ParentCompleted} for ParentRecordId={ParentRecordId}", parentCompleted, task.ParentRecordId);

        if (!parentCompleted) 
        {
            Log.Information("Parent not completed. Skipping execution for TaskId={TaskId}", task.Id);
            return;
        }

        await ExecuteSingleTaskAsync(task, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteSingleTaskAsync(PhoneOrderPushTask task, CancellationToken cancellationToken)
    {
        try
        {
            await _salesDataProvider.MarkSendingAsync(task.Id, true, cancellationToken).ConfigureAwait(false);
            
            switch (task.TaskType)
            {
                case PhoneOrderPushTaskType.GenerateOrder: 
                    await ExecuteGenerateAsync(task, cancellationToken).ConfigureAwait(false);
                    break;

                case PhoneOrderPushTaskType.DeleteOrder:
                    await ExecuteDeleteAsync(task, cancellationToken).ConfigureAwait(false);
                    break;
            }
            
            await _salesDataProvider.MarkSentAsync(task.Id, true, cancellationToken).ConfigureAwait(false);
            
            await TryCompleteRecordAsync(task.RecordId, cancellationToken).ConfigureAwait(false);

            Log.Information("Enqueuing next push task for RecordId={RecordId}", task.RecordId);
            _backgroundJobClient.Enqueue<ISalesPhoneOrderPushService>(s => s.ExecutePhoneOrderPushTasksAsync(task.RecordId, CancellationToken.None));
        }
        catch (Exception ex)
        {
            await _salesDataProvider.MarkFailedAsync(task.Id, true, cancellationToken).ConfigureAwait(false);

            Log.Error(ex, "PhoneOrderPushTask failed. TaskId={TaskId}", task.Id);
        }
    }
    
    private async Task TryCompleteRecordAsync(int recordId, CancellationToken cancellationToken)
    {
        var hasPendingTasks = await _salesDataProvider.HasPendingTasksByRecordIdAsync(recordId, cancellationToken).ConfigureAwait(false);
        Log.Information("HasPendingTasks={HasPendingTasks} for RecordId={RecordId}", hasPendingTasks, recordId);

        if (!hasPendingTasks)
        {
            Log.Information("Marking RecordId={RecordId} as completed", recordId);
            await _phoneOrderDataProvider.MarkRecordCompletedAsync(recordId, true, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Log.Information("Cannot mark RecordId={RecordId} as complete. Pending tasks exist.", recordId);
        }
    }
    
    private async Task ExecuteGenerateAsync(PhoneOrderPushTask task, CancellationToken cancellationToken)
    {
        var req = JsonSerializer.Deserialize<GenerateAiOrdersRequestDto>(task.RequestJson);

        var resp = await _salesClient.GenerateAiOrdersAsync(req, cancellationToken).ConfigureAwait(false);

        if (resp?.Data == null || resp.Data.OrderId == Guid.Empty)
            throw new Exception("GenerateAiOrdersAsync failed");
        
        Log.Information("Sales GenerateOrder SUCCESS. TaskId={TaskId}, Request={@Request}, OrderId={OrderId}", task.Id, req, resp.Data.OrderId);

        await _phoneOrderDataProvider.UpdateOrderIdAsync(task.RecordId, resp.Data.OrderId, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteDeleteAsync(PhoneOrderPushTask task, CancellationToken cancellationToken)
    {
        var req = JsonSerializer.Deserialize<DeleteAiOrderRequestDto>(task.RequestJson);

        await _salesClient.DeleteAiOrderAsync(req, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Sales DeleteOrder SUCCESS. TaskId={TaskId}, Request={@Request}", task.Id, req);
    }
}