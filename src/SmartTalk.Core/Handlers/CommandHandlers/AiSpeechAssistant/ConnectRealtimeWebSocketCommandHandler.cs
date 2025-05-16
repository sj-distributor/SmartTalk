using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class ConnectRealtimeWebSocketCommandHandler : ICommandHandler<ConnectRealtimeWebSocketCommand, ConnectRealtimeWebSocketResponse>
{
    private readonly IAiSpeechAssistantService _speechAssistantService;

    public ConnectRealtimeWebSocketCommandHandler(IAiSpeechAssistantService speechAssistantService)
    {
        _speechAssistantService = speechAssistantService;
    }
    
    public async Task<ConnectRealtimeWebSocketResponse> Handle(IReceiveContext<ConnectRealtimeWebSocketCommand> context, CancellationToken cancellationToken)
    {
        return await _speechAssistantService.ConnectRealTimeWebSocketAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}