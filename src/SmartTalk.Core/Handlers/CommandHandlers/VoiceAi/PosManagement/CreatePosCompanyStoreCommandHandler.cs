using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class CreatePosCompanyStoreCommandHandler : ICommandHandler<CreatePosCompanyStoreCommand, CreatePosCompanyStoreResponse>
{
    private readonly IPosManagementService _posManagementService;

    public CreatePosCompanyStoreCommandHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }

    public async Task<CreatePosCompanyStoreResponse> Handle(IReceiveContext<CreatePosCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.CreatePosCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}