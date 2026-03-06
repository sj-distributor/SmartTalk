using Mediator.Net;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingSyncCallRecordRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;

    public SchedulingSyncCallRecordRecurringJob(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new SyncCallRecordCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingSyncCallRecordRecurringJob);

    public string CronExpression => "0 0 * * *";
}