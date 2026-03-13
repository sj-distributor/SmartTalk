using Mediator.Net;
using SmartTalk.Core.Settings.Jobs;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingSyncAiSpeechAssistantLanguageRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;
    private readonly SchedulingSyncAiSpeechAssistantLanguageRecurringJobExpressionSetting _settings;

    public SchedulingSyncAiSpeechAssistantLanguageRecurringJob(
        IMediator mediator,
        SchedulingSyncAiSpeechAssistantLanguageRecurringJobExpressionSetting settings)
    {
        _mediator = mediator;
        _settings = settings;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new SyncAiSpeechAssistantLanguageCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingSyncAiSpeechAssistantLanguageRecurringJob);

    public string CronExpression => _settings.Value;
}
