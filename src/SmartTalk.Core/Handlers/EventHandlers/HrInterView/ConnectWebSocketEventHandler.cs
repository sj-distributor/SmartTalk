using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.EventHandling;
using SmartTalk.Messages.Events.HrInterView;

namespace SmartTalk.Core.Handlers.EventHandlers.HrInterView;

public class ConnectWebSocketEventHandler : IEventHandler<ConnectWebSocketEvent>
{
    private readonly IEventHandlingService _eventHandlingService;

    public ConnectWebSocketEventHandler(IEventHandlingService eventHandlingService)
    {
        _eventHandlingService = eventHandlingService;
    }

    public async Task Handle(IReceiveContext<ConnectWebSocketEvent> context, CancellationToken cancellationToken)
    {
        await _eventHandlingService.HandlingEventAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}