using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class ManagePosCompanyStoreAccountCommandHandler : ICommandHandler<ManagePosCompanyStoreAccountsCommand, ManagePosCompanyStoreAccountsResponse>
{
    private readonly IPosService _posService;

    public ManagePosCompanyStoreAccountCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<ManagePosCompanyStoreAccountsResponse> Handle(IReceiveContext<ManagePosCompanyStoreAccountsCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.ManagePosCompanyStoreAccountAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}