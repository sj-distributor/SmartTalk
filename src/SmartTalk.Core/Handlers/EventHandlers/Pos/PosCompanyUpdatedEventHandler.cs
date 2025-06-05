using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Events.Pos;

namespace SmartTalk.Core.Handlers.EventHandlers.Pos;

public class PosCompanyUpdatedEventHandler : IEventHandler<PosCompanyUpdatedEvent>
{
    public Task Handle(IReceiveContext<PosCompanyUpdatedEvent> context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}