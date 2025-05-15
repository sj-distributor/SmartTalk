using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Events.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.EventHandlers.VoiceAi.PosManagement;

public class PosCompanyUpdatedEventHandler : IEventHandler<PosCompanyUpdatedEvent>
{
    public Task Handle(IReceiveContext<PosCompanyUpdatedEvent> context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}