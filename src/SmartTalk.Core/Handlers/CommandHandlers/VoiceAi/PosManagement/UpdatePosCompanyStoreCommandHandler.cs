using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class UpdatePosCompanyStoreCommandHandler : ICommandHandler<UpdatePosCompanyStoreCommand, UpdatePosCompanyStoreResponse>
{
    private readonly IPosManagementService _posManagementService;

    public UpdatePosCompanyStoreCommandHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }

    public async Task<UpdatePosCompanyStoreResponse> Handle(IReceiveContext<UpdatePosCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.UpdatePosCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}