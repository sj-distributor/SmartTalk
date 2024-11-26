using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Account;
using SmartTalk.Messages.Requests.Security;

namespace SmartTalk.Core.Handlers.RequestHandlers.Security;

public class GetAccountInfoRequestHandler : IRequestHandler<GetUserAccountInfoRequest, GetUserAccountInfoResponse>
{
    private readonly IAccountService _accountService;
    
    public GetAccountInfoRequestHandler(IAccountService accountService)
    {
        _accountService = accountService;
    }
    
    public async Task<GetUserAccountInfoResponse> Handle(IReceiveContext<GetUserAccountInfoRequest> context, CancellationToken cancellationToken)
    {
        return await _accountService.GetAccountInfoAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}