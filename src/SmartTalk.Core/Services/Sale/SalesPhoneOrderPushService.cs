using System.Text.Json;
using Serilog;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Enums.Sales;

namespace SmartTalk.Core.Services.Sale;

public interface ISalesPhoneOrderPushService : IScopedDependency
{
    Task ExecutePhoneOrderPushTasksAsync(int assistantId, CancellationToken cancellationToken);
}

public class SalesPhoneOrderPushService : ISalesPhoneOrderPushService
{
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly ISalesClient _salesClient;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;

    public SalesPhoneOrderPushService(ISalesDataProvider salesDataProvider, ISalesClient salesClient, IPhoneOrderDataProvider phoneOrderDataProvider)
    {
        _salesDataProvider = salesDataProvider;
        _salesClient = salesClient;
        _phoneOrderDataProvider = phoneOrderDataProvider;
    }

    public async Task ExecutePhoneOrderPushTasksAsync(int assistantId, CancellationToken cancellationToken)
    {
        var tasks = await _salesDataProvider.GetExecutableTasksAsync(assistantId, cancellationToken).ConfigureAwait(false);

        foreach (var task in tasks)
        {
            await ExecuteSingleTaskAsync(task, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteSingleTaskAsync(PhoneOrderPushTask task, CancellationToken cancellationToken)
    {
        try
        {
            var parentCompleted = await _salesDataProvider.IsParentCompletedAsync(task.ParentRecordId, cancellationToken).ConfigureAwait(false);

            if (!parentCompleted) return;
            
            await _salesDataProvider.MarkSendingAsync(task.Id, cancellationToken).ConfigureAwait(false);
            
            switch (task.TaskType)
            {
                case PhoneOrderPushTaskType.GenerateOrder: 
                    await ExecuteGenerateAsync(task, cancellationToken).ConfigureAwait(false);
                    break;

                case PhoneOrderPushTaskType.DeleteOrder:
                    await ExecuteDeleteAsync(task, cancellationToken).ConfigureAwait(false);
                    break;
            }
            
            await _salesDataProvider.MarkSentAsync(task.Id, cancellationToken).ConfigureAwait(false);
            
            await TryCompleteRecordAsync(task.RecordId, cancellationToken).ConfigureAwait(false);
            
            await NotifyChildTasksAsync(task.RecordId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _salesDataProvider.MarkFailedAsync(task.Id, cancellationToken).ConfigureAwait(false);

            Log.Error(ex,
                "PhoneOrderPushTask failed. TaskId={TaskId}", task.Id);
        }
    }
    
    private async Task TryCompleteRecordAsync(int recordId, CancellationToken cancellationToken)
    {
        var hasPendingTasks = await _salesDataProvider.HasPendingTasksByRecordIdAsync(recordId, cancellationToken).ConfigureAwait(false);

        if (!hasPendingTasks)
        {
            await _phoneOrderDataProvider.MarkRecordCompletedAsync(recordId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task NotifyChildTasksAsync(int recordId, CancellationToken cancellationToken)
    {
        var childTasks = await _salesDataProvider.GetTasksByParentRecordIdAsync(recordId, cancellationToken).ConfigureAwait(false);

        foreach (var task in childTasks)
        {
            if (task.Status != PhoneOrderPushTaskStatus.Pending)
                continue;

            await ExecuteSingleTaskAsync(task, cancellationToken).ConfigureAwait(false);
        }
    }

    
    private async Task ExecuteGenerateAsync(PhoneOrderPushTask task, CancellationToken cancellationToken)
    {
        var req = JsonSerializer.Deserialize<GenerateAiOrdersRequestDto>(task.RequestJson);

        var resp = await _salesClient.GenerateAiOrdersAsync(req, cancellationToken).ConfigureAwait(false);

        if (resp?.Data == null || resp.Data.OrderId == Guid.Empty)
            throw new Exception("GenerateAiOrdersAsync failed");

        await _phoneOrderDataProvider.UpdateOrderIdAsync(task.RecordId, resp.Data.OrderId, cancellationToken).ConfigureAwait(false);
    }


    private async Task ExecuteDeleteAsync(PhoneOrderPushTask task, CancellationToken cancellationToken)
    {
        var req = JsonSerializer.Deserialize<DeleteAiOrderRequestDto>(task.RequestJson);

        await _salesClient.DeleteAiOrderAsync(req, cancellationToken).ConfigureAwait(false);
    }
}