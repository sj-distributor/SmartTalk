using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class OpenAiAccountTrainingCommandHandler : ICommandHandler<OpenAiAccountTrainingCommand>
{
    private readonly IAiSpeechAssistantProcessJobService _aiSpeechAssistantProcessJobService;

    public OpenAiAccountTrainingCommandHandler(IAiSpeechAssistantProcessJobService assistantProcessJobService)
    {
        _aiSpeechAssistantProcessJobService = assistantProcessJobService;
    }

    public async Task Handle(IReceiveContext<OpenAiAccountTrainingCommand> context, CancellationToken cancellationToken)
    {
        await _aiSpeechAssistantProcessJobService.OpenAiAccountTrainingAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}