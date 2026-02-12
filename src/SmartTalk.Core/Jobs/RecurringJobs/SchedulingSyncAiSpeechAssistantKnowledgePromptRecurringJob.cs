using Hangfire;
using Mediator.Net;
using SmartTalk.Core.Settings.Jobs;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingSyncAiSpeechAssistantKnowledgePromptRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;

    public SchedulingSyncAiSpeechAssistantKnowledgePromptRecurringJob(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new SyncAiSpeechAssistantKnowledgePromptCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingSyncAiSpeechAssistantKnowledgePromptRecurringJob);

    public string CronExpression => "*/5 * * * *";
}

