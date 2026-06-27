using Hangfire;
using Mediator.Net;
using SmartTalk.Core.Settings.AiResourceSync;
using SmartTalk.Core.Settings.Jobs;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Commands.AiResourceSync;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingSyncCrmSalesAutoCreateRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;
    private readonly SchedulingAiResourceSyncRecurringJobCronExpression _setting;

    public SchedulingSyncCrmSalesAutoCreateRecurringJob(IMediator mediator, SchedulingAiResourceSyncRecurringJobCronExpression setting)
    {
        _mediator = mediator;
        _setting = setting;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new SchedulingAiResourceSyncCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingSyncCrmSalesAutoCreateRecurringJob);

    public string CronExpression => string.IsNullOrWhiteSpace(_setting.Value) ? Cron.Never() : _setting.Value;

    public TimeZoneInfo TimeZone => PstTimeZone.Get();
}
