using Hangfire;
using Mediator.Net;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingSyncAiSpeechAssistantKnowledgeDetailRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;

    public SchedulingSyncAiSpeechAssistantKnowledgeDetailRecurringJob(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new SyncAiSpeechAssistantKnowledgeDetailCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingSyncAiSpeechAssistantKnowledgeDetailRecurringJob);

    public string CronExpression => Cron.Never();
}
