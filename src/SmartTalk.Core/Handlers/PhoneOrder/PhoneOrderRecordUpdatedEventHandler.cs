using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.EventHandling;
using SmartTalk.Messages.Events.PhoneOrder;

namespace SmartTalk.Core.Handlers.PhoneOrder;

public class PhoneOrderRecordUpdatedEventHandler : IEventHandler<PhoneOrderRecordUpdatedEvent>
{
    private readonly IEventHandlingService _eventHandlingService;

    public PhoneOrderRecordUpdatedEventHandler(IEventHandlingService eventHandlingService)
    {
        _eventHandlingService = eventHandlingService;
    }

    public async Task Handle(IReceiveContext<PhoneOrderRecordUpdatedEvent> context, CancellationToken cancellationToken)
    {
        await _eventHandlingService.HandlingEventAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}