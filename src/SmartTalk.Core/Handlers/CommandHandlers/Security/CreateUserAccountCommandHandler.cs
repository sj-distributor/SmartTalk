using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Account;
using SmartTalk.Messages.Commands.Security;

namespace SmartTalk.Core.Handlers.CommandHandlers.Security;

public class CreateUserAccountCommandHandler : ICommandHandler<CreateUserAccountCommand, CreateUserAccountResponse>
{
    private readonly IAccountService _accountService;

    public CreateUserAccountCommandHandler(IAccountService accountService)
    {
        _accountService = accountService;
    }

    public async Task<CreateUserAccountResponse> Handle(IReceiveContext<CreateUserAccountCommand> context, CancellationToken cancellationToken)
    {
        return await _accountService.CreateUserAccountAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}