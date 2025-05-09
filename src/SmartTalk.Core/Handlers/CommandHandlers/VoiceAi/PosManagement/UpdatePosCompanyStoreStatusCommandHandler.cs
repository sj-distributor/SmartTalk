using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class UpdatePosCompanyStoreStatusCommandHandler : ICommandHandler<UpdatePosCompanyStoreStatusCommand, UpdatePosCompanyStoreStatusResponse>
{
    private readonly IPosManagementService _posManagementService;

    public UpdatePosCompanyStoreStatusCommandHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }

    public async Task<UpdatePosCompanyStoreStatusResponse> Handle(IReceiveContext<UpdatePosCompanyStoreStatusCommand> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.UpdatePosCompanyStoreStatusAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}