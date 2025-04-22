using Hangfire;
using Mediator.Net;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class ScheduingTrainingOpenAiAccountRecurringJob: IRecurringJob
{
    private readonly IMediator _mediator;
    private readonly OpenAiAccountTrainingSettings _openAiAccountTrainingSettings;

    public ScheduingTrainingOpenAiAccountRecurringJob(
        IMediator mediator, OpenAiAccountTrainingSettings openAiAccountTrainingSettings)
    {
        _mediator = mediator;
        _openAiAccountTrainingSettings = openAiAccountTrainingSettings;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new OpenAiAccountTrainingCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(ScheduingTrainingOpenAiAccountRecurringJob);

    public string CronExpression => _openAiAccountTrainingSettings.OpenAiTrainingCronExpression;
}