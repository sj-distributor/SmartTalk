using Hangfire;
using Serilog;
using SmartTalk.Core.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Sale;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingRepairPhoneOrderCompletionRecurringJob : IRecurringJob
{
    private const int BatchSize = 200;

    private readonly ISalesDataProvider _salesDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;

    public SchedulingRepairPhoneOrderCompletionRecurringJob(
        ISalesDataProvider salesDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider)
    {
        _salesDataProvider = salesDataProvider;
        _phoneOrderDataProvider = phoneOrderDataProvider;
    }

    public async Task Execute()
    {
        var recordIds = await _salesDataProvider
            .GetIncompleteRecordsWithAllTasksSentAsync(BatchSize, CancellationToken.None)
            .ConfigureAwait(false);

        foreach (var recordId in recordIds)
        {
            await _phoneOrderDataProvider
                .MarkRecordCompletedAsync(recordId, CancellationToken.None)
                .ConfigureAwait(false);
        }

        if (recordIds.Count > 0)
        {
            Log.Information("Repaired incomplete phone order records. Count={Count}, RecordIds={RecordIds}",
                recordIds.Count, recordIds);
        }
    }

    public string JobId => nameof(SchedulingRepairPhoneOrderCompletionRecurringJob);
    public string CronExpression => Cron.Daily();
}
