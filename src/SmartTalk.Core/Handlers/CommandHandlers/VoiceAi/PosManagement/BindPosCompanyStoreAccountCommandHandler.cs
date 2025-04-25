using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class BindPosCompanyStoreAccountCommandHandler : ICommandHandler<BindPosCompanyStoreAccountsCommand, BindPosCompanyStoreAccountsResponse>
{
    private readonly IPosManagementService _posManagementService;

    public BindPosCompanyStoreAccountCommandHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }

    public async Task<BindPosCompanyStoreAccountsResponse> Handle(IReceiveContext<BindPosCompanyStoreAccountsCommand> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.BindPosCompanyStoreAccountAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}