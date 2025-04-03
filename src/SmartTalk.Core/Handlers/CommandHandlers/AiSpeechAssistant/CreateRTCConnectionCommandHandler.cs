using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class CreateRTCConnectionCommandHandler : ICommandHandler<CreateRealtimeConnectionCommand, CreateRealtimeConnectionResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public CreateRTCConnectionCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<CreateRealtimeConnectionResponse> Handle(IReceiveContext<CreateRealtimeConnectionCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.CreateRealtimeConnectionAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}