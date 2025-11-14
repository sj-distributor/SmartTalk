using Hangfire;
using Mediator.Net;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingSyncAiSpeechAssistantInfoToAgentRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;

    public SchedulingSyncAiSpeechAssistantInfoToAgentRecurringJob(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task Execute()
    {
        await _mediator.SendAsync(new SyncAiSpeechAssistantInfoToAgentCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingSyncAiSpeechAssistantInfoToAgentRecurringJob);

    public string CronExpression => Cron.Never();
}