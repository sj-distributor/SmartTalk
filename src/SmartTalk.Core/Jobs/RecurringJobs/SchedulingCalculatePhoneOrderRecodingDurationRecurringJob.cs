using Hangfire;
using Mediator.Net;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingCalculatePhoneOrderRecodingDurationRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;

    public SchedulingCalculatePhoneOrderRecodingDurationRecurringJob(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new SchedulingCalculatePhoneOrderRecodingDurationCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingCalculatePhoneOrderRecodingDurationRecurringJob);
    
    public string CronExpression => Cron.Never();
}