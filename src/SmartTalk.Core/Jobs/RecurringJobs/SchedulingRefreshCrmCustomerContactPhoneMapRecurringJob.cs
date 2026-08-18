using Hangfire;
using Mediator.Net;
using SmartTalk.Core.Settings.AiResourceSync;
using SmartTalk.Core.Settings.Jobs;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Commands.AiResourceSync;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingRefreshCrmCustomerContactPhoneMapRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;
    private readonly SchedulingRefreshCrmCustomerContactPhoneMapRecurringJobCronExpression _setting;

    public SchedulingRefreshCrmCustomerContactPhoneMapRecurringJob(IMediator mediator, SchedulingRefreshCrmCustomerContactPhoneMapRecurringJobCronExpression setting)
    {
        _mediator = mediator;
        _setting = setting;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new SchedulingRefreshCrmCustomerContactPhoneMapCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingRefreshCrmCustomerContactPhoneMapRecurringJob);

    public string CronExpression => string.IsNullOrWhiteSpace(_setting.Value) ? Cron.Never() : _setting.Value;

    public TimeZoneInfo TimeZone => PstTimeZone.Get();
}
