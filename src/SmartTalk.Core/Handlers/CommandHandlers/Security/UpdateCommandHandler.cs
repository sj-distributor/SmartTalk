using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Security;
using SmartTalk.Messages.Commands.Security;

namespace SmartTalk.Core.Handlers.CommandHandlers.Security;

public class AuthorityCommandHandler : ICommandHandler<UpdateUserAccountCommand, UpdateUserAccountResponse>
{
    private readonly ISecurityService _securityService;
    
    public AuthorityCommandHandler(ISecurityService securityService)
    {
        _securityService = securityService;
    }
    
    public async Task<UpdateUserAccountResponse> Handle(IReceiveContext<UpdateUserAccountCommand> context, CancellationToken cancellationToken)
    {
        return await _securityService.UpdateRoleUserAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}