using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class DeletePosCompanyStoreCommandHandler : ICommandHandler<DeletePosCompanyStoreCommand, DeletePosCompanyStoreResponse>
{
    private readonly IPosManagementService _posManagementService;

    public DeletePosCompanyStoreCommandHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }

    public async Task<DeletePosCompanyStoreResponse> Handle(IReceiveContext<DeletePosCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.DeletePosCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}