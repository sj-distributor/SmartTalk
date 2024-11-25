using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Account;
using SmartTalk.Messages.Commands.Authority;

namespace SmartTalk.Core.Handlers.CommandHandlers.Authority;

public class DeleteAccountsCommandHandler : ICommandHandler<DeleteAccountsCommand, DeleteAccountsResponse>
{
    private readonly IAccountService _accountService;
    
    public DeleteAccountsCommandHandler(IAccountService accountService)
    {
        _accountService = accountService;
    }
    
    public async Task<DeleteAccountsResponse> Handle(IReceiveContext<DeleteAccountsCommand> context, CancellationToken cancellationToken)
    {
        return await _accountService.DeleteUserAccountAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}