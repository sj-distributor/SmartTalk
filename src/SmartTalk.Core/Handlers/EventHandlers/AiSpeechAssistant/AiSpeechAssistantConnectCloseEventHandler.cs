using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Events.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.EventHandlers.AiSpeechAssistant;

public class AiSpeechAssistantConnectCloseEventHandler : IEventHandler<AiSpeechAssistantConnectCloseEvent>
{
    public Task Handle(IReceiveContext<AiSpeechAssistantConnectCloseEvent> context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}