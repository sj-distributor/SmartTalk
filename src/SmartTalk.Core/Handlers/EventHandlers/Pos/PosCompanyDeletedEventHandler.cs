using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Events.Pos;

namespace SmartTalk.Core.Handlers.EventHandlers.Pos;

public class PosCompanyDeletedEventHandler : IEventHandler<PosCompanyDeletedEvent>
{
    public Task Handle(IReceiveContext<PosCompanyDeletedEvent> context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}