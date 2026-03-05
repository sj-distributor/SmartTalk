using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Security;
using SmartTalk.Messages.Requests.Security;

namespace SmartTalk.Core.Handlers.CommandHandlers.Security;

public class GetPermissionsByPermissionLevelRequestHandler : IRequestHandler<GetPermissionsByPermissionLevelRequest, GetPermissionsByPermissionLevelResponse>
{
    private readonly ISecurityService _securityService;

    public GetPermissionsByPermissionLevelRequestHandler(ISecurityService securityService)
    {
        _securityService = securityService;
    }
    
    public async Task<GetPermissionsByPermissionLevelResponse> Handle(IReceiveContext<GetPermissionsByPermissionLevelRequest> context, CancellationToken cancellationToken)
    {
        return await _securityService.GetPermissionsByPermissionLevelAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}