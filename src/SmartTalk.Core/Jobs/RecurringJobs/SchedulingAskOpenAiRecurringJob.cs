using Hangfire;
using Mediator.Net;
using Smarties.Messages.Commands.Warehouse;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Commands.Security;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingAskOpenAiRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;
    private readonly OpenAiTrainingSettings _openAiTrainingSettings;

    public SchedulingAskOpenAiRecurringJob(
        IMediator mediator, OpenAiTrainingSettings openAiTrainingSettings)
    {
        _mediator = mediator;
        _openAiTrainingSettings = openAiTrainingSettings;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new OpenAiAccountTrainingCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingAskOpenAiRecurringJob);

    public string CronExpression => _openAiTrainingSettings.OpenAiTrainingCronExpression;
}