using Hangfire;
using Mediator.Net;
using SmartTalk.Messages.Commands.SpeechMatics;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingUpdateSpeechMaticsKeysRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;

    public SchedulingUpdateSpeechMaticsKeysRecurringJob(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task Execute()
    {
        await _mediator.SendAsync(new SchedulingUpdateSpeechMaticsKeysCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingUpdateSpeechMaticsKeysRecurringJob);

    public string CronExpression => Cron.Monthly();
}