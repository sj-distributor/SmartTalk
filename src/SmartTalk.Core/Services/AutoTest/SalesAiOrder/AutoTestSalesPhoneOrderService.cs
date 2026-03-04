using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Enums.AutoTest;
using SmartTalk.Messages.Commands.AutoTest.SalesPhoneOrder;

namespace SmartTalk.Core.Services.AutoTest.SalesAiOrder;

public interface IAutoTestSalesPhoneOrderService : IScopedDependency
{
    Task ExecuteSalesPhoneOrderTestAsync(ExecuteSalesPhoneOrderTestCommand command, CancellationToken cancellationToken);
}

public class AutoTestSalesPhoneOrderService : IAutoTestSalesPhoneOrderService
{
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public AutoTestSalesPhoneOrderService(IAutoTestDataProvider autoTestDataProvider, ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _autoTestDataProvider = autoTestDataProvider;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task ExecuteSalesPhoneOrderTestAsync(ExecuteSalesPhoneOrderTestCommand command, CancellationToken cancellationToken)
    {
        var task = await _autoTestDataProvider.GetAutoTestTaskByIdAsync(command.TaskId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Ready to execute this sales phone order test task: {@Task}", task);

        if (task is not { Status: AutoTestTaskStatus.Ongoing }) return;
        
        var records = await _autoTestDataProvider.GetStatusTaskRecordsByTaskIdAsync(command.TaskId, AutoTestTaskRecordStatus.Ongoing, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Processing task record: {@Records}", records);
        
        if (records == null || records.Count == 0) return;

        foreach (var record in records)
        {
            _backgroundJobClient.Enqueue<IAutoTestSalesPhoneOrderProcessJobService>(x => x.StartTestingSalesPhoneOrderTaskAsync(task.Id, record.Id, cancellationToken));
        }
    }
}