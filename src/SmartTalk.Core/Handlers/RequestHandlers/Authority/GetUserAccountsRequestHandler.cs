using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Account;
using SmartTalk.Messages.Requests.Authority;

namespace SmartTalk.Core.Handlers.RequestHandlers.Authority;

public class GetUserAccountsRequestHandler : IRequestHandler<GetUserAccountsRequest, GetUserAccountsResponse>
{
    private readonly IAccountService _accountService;
    
    public GetUserAccountsRequestHandler(IAccountService accountService)
    {
        _accountService = accountService;
    }
    
    public async Task<GetUserAccountsResponse> Handle(IReceiveContext<GetUserAccountsRequest> context, CancellationToken cancellationToken)
    {
        return await _accountService.GetAccountsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}