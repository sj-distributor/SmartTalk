using Hangfire;
using Mediator.Net;
using SmartTalk.Messages.Commands.SipServer;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingBackupSipServerDataRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;

    public SchedulingBackupSipServerDataRecurringJob(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task Execute()
    {
        await _mediator.SendAsync(new BackupSipServerDataCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingBackupSipServerDataRecurringJob);

    public string CronExpression => Cron.Daily();
}