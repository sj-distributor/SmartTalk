using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Security;
using SmartTalk.Messages.Requests.Security;

namespace SmartTalk.Core.Handlers.RequestHandlers.Security;

public class GetPermissionsByRoleIdRequestHandler : IRequestHandler<GetPermissionsByRoleIdRequest, GetPermissionsByRoleIdResponse>
{
    private readonly ISecurityService _securityService;

    public GetPermissionsByRoleIdRequestHandler(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    public async Task<GetPermissionsByRoleIdResponse> Handle(IReceiveContext<GetPermissionsByRoleIdRequest> context, CancellationToken cancellationToken)
    {
        return await _securityService.GetPermissionsByRoleIdAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}