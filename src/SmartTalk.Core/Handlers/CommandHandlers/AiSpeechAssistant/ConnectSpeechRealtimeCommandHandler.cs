using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class ConnectSpeechRealtimeCommandHandler : ICommandHandler<ConnectSpeechRealtimeCommand>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public ConnectSpeechRealtimeCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task Handle(IReceiveContext<ConnectSpeechRealtimeCommand> context, CancellationToken cancellationToken)
    {
        await _aiSpeechAssistantService.ConnectSpeechRealtimeAsync(context.Message, cancellationToken);
    }
}