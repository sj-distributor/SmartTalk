using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class ManageCompanyStoreAccountCommandHandler : ICommandHandler<ManageCompanyStoreAccountsCommand, ManageCompanyStoreAccountsResponse>
{
    private readonly IPosService _posService;

    public ManageCompanyStoreAccountCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<ManageCompanyStoreAccountsResponse> Handle(IReceiveContext<ManageCompanyStoreAccountsCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.ManageCompanyStoreAccountAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}