using Mediator.Net;
using SmartTalk.Core.Settings.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingPhoneOrderDailyDataBroadcastRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;
    private readonly SchedulingPhoneOrderDailyDataBroadcastRecurringJobExpressionSetting _cronExpressionSetting;

    public SchedulingPhoneOrderDailyDataBroadcastRecurringJob(IMediator mediator, SchedulingPhoneOrderDailyDataBroadcastRecurringJobExpressionSetting cronExpressionSetting)
    {
        _mediator = mediator;
        _cronExpressionSetting = cronExpressionSetting;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new SchedulingPhoneOrderDailyDataBroadcastCommand{ RobotUrl = _cronExpressionSetting.RobotUrl }).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingPhoneOrderDailyDataBroadcastRecurringJob);
    
    public string CronExpression => _cronExpressionSetting.Value;
    
    public TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
}