using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Account;
using SmartTalk.Messages.Requests.Authority;

namespace SmartTalk.Core.Handlers.RequestHandlers.Authority;

public class GetAccountInfoRequestHandler : IRequestHandler<GetAccountInfoRequest, GetAccountInfoResponse>
{
    private readonly IAccountService _accountService;
    
    public GetAccountInfoRequestHandler(IAccountService accountService)
    {
        _accountService = accountService;
    }
    
    public async Task<GetAccountInfoResponse> Handle(IReceiveContext<GetAccountInfoRequest> context, CancellationToken cancellationToken)
    {
        return await _accountService.GetAccountInfoAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}