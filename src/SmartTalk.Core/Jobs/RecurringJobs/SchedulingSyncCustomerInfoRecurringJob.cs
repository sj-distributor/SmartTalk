using Hangfire;
using Mediator.Net;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingSyncCustomerInfoRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;

    public SchedulingSyncCustomerInfoRecurringJob(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task Execute()
    {
        await _mediator.SendAsync(new SchedulingSyncCustomerInfoCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingSyncCustomerInfoRecurringJob);

    public string CronExpression => Cron.Never();
}