using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.EventHandling;
using SmartTalk.Messages.Events.Pos;

namespace SmartTalk.Core.Handlers.EventHandlers.Pos;

public class PosOrderPlacedEventHandler : IEventHandler<PosOrderPlacedEvent>
{
    private readonly IEventHandlingService _eventHandlingService;

    public PosOrderPlacedEventHandler(IEventHandlingService eventHandlingService)
    {
        _eventHandlingService = eventHandlingService;
    }

    public async Task Handle(IReceiveContext<PosOrderPlacedEvent> context, CancellationToken cancellationToken)
    {
        await _eventHandlingService.HandlingEventAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}