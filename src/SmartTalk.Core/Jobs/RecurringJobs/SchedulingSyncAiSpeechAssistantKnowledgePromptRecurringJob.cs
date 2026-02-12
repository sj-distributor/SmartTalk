using Mediator.Net;
using SmartTalk.Core.Settings.Jobs;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingSyncAiSpeechAssistantKnowledgePromptRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;
    private readonly SchedulingSyncAiSpeechAssistantKnowledgePromptRecurringJobExpressionSetting _settings;

    public SchedulingSyncAiSpeechAssistantKnowledgePromptRecurringJob(
        IMediator mediator,
        SchedulingSyncAiSpeechAssistantKnowledgePromptRecurringJobExpressionSetting settings)
    {
        _mediator = mediator;
        _settings = settings;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new SyncAiSpeechAssistantKnowledgePromptCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingSyncAiSpeechAssistantKnowledgePromptRecurringJob);

    public string CronExpression => _settings.Value;
}
