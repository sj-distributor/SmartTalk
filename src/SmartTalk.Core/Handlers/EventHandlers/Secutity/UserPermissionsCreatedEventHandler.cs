using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Events.Security;

namespace SmartTalk.Core.Handlers.EventHandlers.Security;

public class UserPermissionsCreatedEventHandler : IEventHandler<UserPermissionsCreatedEvent>
{
    public Task Handle(IReceiveContext<UserPermissionsCreatedEvent> context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}