using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Security;
using SmartTalk.Messages.Commands.Security;

namespace SmartTalk.Core.Handlers.CommandHandlers.Security;

public class UpdateUserAccountTaskNotificationCommandHandler : ICommandHandler<UpdateUserAccountTaskNotificationCommand, UpdateUserAccountTaskNotificationResponse>
{
    private readonly ISecurityService _securityService;
    
    public UpdateUserAccountTaskNotificationCommandHandler(ISecurityService securityService)
    {
        _securityService = securityService;
    }
    
    public async Task<UpdateUserAccountTaskNotificationResponse> Handle(IReceiveContext<UpdateUserAccountTaskNotificationCommand> context, CancellationToken cancellationToken)
    {
        return await _securityService.UpdateUserAccountTaskNotificationAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}