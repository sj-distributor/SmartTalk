using Hangfire;
using Mediator.Net;
using SmartTalk.Messages.Commands.Linphone;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingAutoGetLinphoneCdrRecordJob : IRecurringJob
{
    private readonly IMediator _mediator;
    
    public SchedulingAutoGetLinphoneCdrRecordJob(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task Execute()
    {
        await _mediator.SendAsync(new SchedulingAutoGetLinphoneCdrRecordCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingAutoGetLinphoneCdrRecordJob);
    public string CronExpression => Cron.Minutely();
}