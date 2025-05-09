using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Events.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.EventHandlers.VoiceAi.PosManagement;

public class PosCompanyDeletedEventHandler : IEventHandler<PosCompanyDeletedEvent>
{
    public Task Handle(IReceiveContext<PosCompanyDeletedEvent> context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}