using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Events.Pos;

namespace SmartTalk.Core.Handlers.EventHandlers.Pos;

public class PosCompanyUpdatedStatusEventHandler : IEventHandler<PosCompanyUpdatedStatusEvent>
{
    public Task Handle(IReceiveContext<PosCompanyUpdatedStatusEvent> context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}