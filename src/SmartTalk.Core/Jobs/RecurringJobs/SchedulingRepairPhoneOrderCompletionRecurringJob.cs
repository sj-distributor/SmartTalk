using Hangfire;
using Serilog;
using SmartTalk.Core.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Utils;

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
        var now = DateTimeOffset.UtcNow;
        var endTime = TimeZoneInfo.ConvertTime(now, PstTimeZone.Get());
        var startTime = endTime.AddDays(-1);

        var recordIds = await _salesDataProvider
            .GetIncompleteRecordsWithAllTasksSentAsync(startTime, endTime, BatchSize, CancellationToken.None)
            .ConfigureAwait(false);

        foreach (var recordId in recordIds)
        {
            await _phoneOrderDataProvider
                .MarkRecordCompletedAsync(recordId, CancellationToken.None)
                .ConfigureAwait(false);
        }

        if (recordIds.Count > 0)
        {
            Log.Information("Repaired incomplete phone order records. Count={Count}, StartTime={StartTime}, EndTime={EndTime}, RecordIds={RecordIds}",
                recordIds.Count, startTime, endTime, recordIds);
        }
    }

    public string JobId => nameof(SchedulingRepairPhoneOrderCompletionRecurringJob);
    public string CronExpression => Cron.Daily();
}
