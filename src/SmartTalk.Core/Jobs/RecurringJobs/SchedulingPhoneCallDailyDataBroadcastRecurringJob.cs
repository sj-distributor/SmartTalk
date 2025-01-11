using Mediator.Net;
using SmartTalk.Core.Settings.PhoneCall;
using SmartTalk.Messages.Commands.PhoneCall;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingPhoneCallDailyDataBroadcastRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;
    private readonly SchedulingPhoneCallDailyDataBroadcastRecurringJobExpressionSetting _cronExpressionSetting;

    public SchedulingPhoneCallDailyDataBroadcastRecurringJob(IMediator mediator, SchedulingPhoneCallDailyDataBroadcastRecurringJobExpressionSetting cronExpressionSetting)
    {
        _mediator = mediator;
        _cronExpressionSetting = cronExpressionSetting;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new SchedulingPhoneCallDailyDataBroadcastCommand{ RobotUrl = _cronExpressionSetting.RobotUrl }).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingPhoneCallDailyDataBroadcastRecurringJob);
    
    public string CronExpression => _cronExpressionSetting.Value;
    
    public TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
}