using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Events.Pos;

namespace SmartTalk.Core.Handlers.EventHandlers.Pos;

public class PosCompanyCreatedEventHandler : IEventHandler<PosCompanyCreatedEvent>
{
    public Task Handle(IReceiveContext<PosCompanyCreatedEvent> context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}