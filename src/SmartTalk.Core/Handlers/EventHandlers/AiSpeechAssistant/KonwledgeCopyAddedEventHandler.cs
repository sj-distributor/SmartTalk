using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.EventHandling;
using SmartTalk.Messages.Events.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.EventHandlers.AiSpeechAssistant;

public class KonwledgeCopyAddedEventHandler : IEventHandler<KonwledgeCopyAddedEvent>
{
    private readonly IEventHandlingService _eventHandlingService;

    public KonwledgeCopyAddedEventHandler(IEventHandlingService eventHandlingService)
    {
        _eventHandlingService = eventHandlingService;
    }

    public async Task Handle(IReceiveContext<KonwledgeCopyAddedEvent> context, CancellationToken cancellationToken)
    {
        await _eventHandlingService.HandlingEventAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}