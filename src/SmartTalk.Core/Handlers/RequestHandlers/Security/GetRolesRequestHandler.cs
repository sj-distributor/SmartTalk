using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Security;
using SmartTalk.Messages.Requests.Security;

namespace SmartTalk.Core.Handlers.RequestHandlers.Security;

public class GetRolesRequestHandler : IRequestHandler<GetRolesRequest, GetRolesResponse>
{
    private readonly ISecurityService _securityService;
    
    public GetRolesRequestHandler(ISecurityService securityService)
    {
        _securityService = securityService;
    }
    
    public async Task<GetRolesResponse> Handle(IReceiveContext<GetRolesRequest> context, CancellationToken cancellationToken)
    {
        return await _securityService.GetRolesAsync(context.Message, cancellationToken);
    }
}