using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class BindPosCompanyStoreCommandHandler : ICommandHandler<BindPosCompanyStoreCommand, BindPosCompanyStoreResponse>
{
    private readonly IPosManagementService _posManagementService;

    public BindPosCompanyStoreCommandHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }
    public async Task<BindPosCompanyStoreResponse> Handle(IReceiveContext<BindPosCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.BindPosCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}