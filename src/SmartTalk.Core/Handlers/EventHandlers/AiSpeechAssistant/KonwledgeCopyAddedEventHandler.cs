using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.EventHandling;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Events.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.EventHandlers.AiSpeechAssistant;

public class KonwledgeCopyAddedEventHandler : IEventHandler<AiSpeechAssistantKonwledgeCopyAddedEvent>
{
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;

    public KonwledgeCopyAddedEventHandler(ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _smartTalkBackgroundJobClient = backgroundJobClient;
    }

    public async Task Handle(IReceiveContext<AiSpeechAssistantKonwledgeCopyAddedEvent> context, CancellationToken cancellationToken)
    {
        _smartTalkBackgroundJobClient.Enqueue<IEventHandlingService>(x => x.HandlingEventAsync(context.Message, cancellationToken));
    }
}