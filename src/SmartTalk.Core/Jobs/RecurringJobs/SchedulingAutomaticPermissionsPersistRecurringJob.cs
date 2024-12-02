using Hangfire;
using Mediator.Net;
using SmartTalk.Messages.Commands.Security;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingAutomaticPermissionsPersistRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;
    
    public SchedulingAutomaticPermissionsPersistRecurringJob(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task Execute()
    {
        await _mediator.SendAsync(new AutomaticPermissionsPersistCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingAutomaticPermissionsPersistRecurringJob);

    public string CronExpression => Cron.Minutely();
}