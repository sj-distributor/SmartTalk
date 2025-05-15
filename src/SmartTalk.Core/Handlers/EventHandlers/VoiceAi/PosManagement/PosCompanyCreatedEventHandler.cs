using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Events.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.EventHandlers.VoiceAi.PosManagement;

public class PosCompanyCreatedEventHandler : IEventHandler<PosCompanyCreatedEvent>
{
    public Task Handle(IReceiveContext<PosCompanyCreatedEvent> context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}