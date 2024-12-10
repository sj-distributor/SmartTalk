using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Security;
using SmartTalk.Messages.Commands.Security;

namespace SmartTalk.Core.Handlers.CommandHandlers.Security;

public class CreateUserPermissionsCommandHandler : ICommandHandler<CreateUserPermissionsCommand, CreateUserPermissionsResponse>
{
    private readonly ISecurityService _securityService;

    public CreateUserPermissionsCommandHandler(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    public async Task<CreateUserPermissionsResponse> Handle(IReceiveContext<CreateUserPermissionsCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _securityService.CreateUserPermissionsAsync(context.Message, cancellationToken).ConfigureAwait(false);
        
        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new CreateUserPermissionsResponse { Data = @event.UserPermissions };
    }
}