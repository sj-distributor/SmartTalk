using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Security;
using SmartTalk.Messages.Requests.Security;

namespace SmartTalk.Core.Handlers.RequestHandlers.Security;

public class GetCurrentUserRolesRequestHandler : IRequestHandler<GetCurrentUserRolesRequest, GetCurrentUserRolesResponse>
{
    private readonly ISecurityService _securityService;

    public GetCurrentUserRolesRequestHandler(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    public async Task<GetCurrentUserRolesResponse> Handle(IReceiveContext<GetCurrentUserRolesRequest> context, CancellationToken cancellationToken)
    {
        return await _securityService.GetCurrentUserRoleAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}