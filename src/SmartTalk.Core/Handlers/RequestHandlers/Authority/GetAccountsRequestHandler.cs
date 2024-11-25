using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Account;
using SmartTalk.Messages.Requests.Authority;

namespace SmartTalk.Core.Handlers.RequestHandlers.Authority;

public class GetAccountsRequestHandler : IRequestHandler<GetAccountsRequest, GetAccountsResponse>
{
    private readonly IAccountService _accountService;
    
    public GetAccountsRequestHandler(IAccountService accountService)
    {
        _accountService = accountService;
    }
    
    public async Task<GetAccountsResponse> Handle(IReceiveContext<GetAccountsRequest> context, CancellationToken cancellationToken)
    {
        return await _accountService.GetAccountsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}