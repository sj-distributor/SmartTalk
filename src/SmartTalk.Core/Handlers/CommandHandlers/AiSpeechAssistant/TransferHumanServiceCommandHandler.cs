using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class TransferHumanServiceCommandHandler : ICommandHandler<TransferHumanServiceCommand>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public TransferHumanServiceCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task Handle(IReceiveContext<TransferHumanServiceCommand> context, CancellationToken cancellationToken)
    {
        await _aiSpeechAssistantService.TransferHumanServiceAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}