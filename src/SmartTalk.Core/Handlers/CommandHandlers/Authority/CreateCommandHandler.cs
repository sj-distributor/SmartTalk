using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Account;
using SmartTalk.Messages.Commands.Account;

namespace SmartTalk.Core.Handlers.CommandHandlers.Account;

public class CreateCommandHandler : ICommandHandler<CreateUserAccountCommand, CreateUserAccountResponse>
{
    private readonly IAccountService _accountService;

    public CreateCommandHandler(IAccountService accountService)
    {
        _accountService = accountService;
    }

    public async Task<CreateUserAccountResponse> Handle(IReceiveContext<CreateUserAccountCommand> context, CancellationToken cancellationToken)
    {
        return await _accountService.CreateUserAccount(context.Message, cancellationToken).ConfigureAwait(false);
    }
}