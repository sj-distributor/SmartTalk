using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Account;
using SmartTalk.Messages.Commands.Authority;

namespace SmartTalk.Core.Handlers.CommandHandlers.Authority;

public class GetAccountsCommandHandler : ICommandHandler<GetAccountsCommand, GetAccountsResponse>
{
    private readonly IAccountService _accountService;
    
    public GetAccountsCommandHandler(IAccountService accountService)
    {
        _accountService = accountService;
    }
    
    public async Task<GetAccountsResponse> Handle(IReceiveContext<GetAccountsCommand> context, CancellationToken cancellationToken)
    {
        return await _accountService.GetAccountsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}