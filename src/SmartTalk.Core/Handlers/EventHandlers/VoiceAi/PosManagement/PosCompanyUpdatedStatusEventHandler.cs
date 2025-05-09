using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Events.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.EventHandlers.VoiceAi.PosManagement;

public class PosCompanyUpdatedStatusEventHandler : IEventHandler<PosCompanyUpdatedStatusEvent>
{
    public Task Handle(IReceiveContext<PosCompanyUpdatedStatusEvent> context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}