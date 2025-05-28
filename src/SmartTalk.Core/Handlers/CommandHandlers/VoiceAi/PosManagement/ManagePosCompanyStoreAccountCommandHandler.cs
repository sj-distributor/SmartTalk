using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class ManagePosCompanyStoreAccountCommandHandler : ICommandHandler<ManagePosCompanyStoreAccountsCommand, ManagePosCompanyStoreAccountsResponse>
{
    private readonly IPosManagementService _posManagementService;

    public ManagePosCompanyStoreAccountCommandHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }

    public async Task<ManagePosCompanyStoreAccountsResponse> Handle(IReceiveContext<ManagePosCompanyStoreAccountsCommand> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.ManagePosCompanyStoreAccountAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}