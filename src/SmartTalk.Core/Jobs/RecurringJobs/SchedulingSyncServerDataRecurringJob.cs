using Hangfire;
using Mediator.Net;
using SmartTalk.Messages.Commands.FilesSynchronize;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingSyncServerDataRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;

    public SchedulingSyncServerDataRecurringJob(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task Execute()
    {
        await _mediator.SendAsync(new SynchronizeFilesCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingSyncServerDataRecurringJob);

    public string CronExpression => Cron.Daily();
}