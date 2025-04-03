using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class CreateRTCConnectionCommandHandler : ICommandHandler<CreateRTCConnectionCommand, CreateRTCConnectionResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public CreateRTCConnectionCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<CreateRTCConnectionResponse> Handle(IReceiveContext<CreateRTCConnectionCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.CreateRTCConnectionAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}