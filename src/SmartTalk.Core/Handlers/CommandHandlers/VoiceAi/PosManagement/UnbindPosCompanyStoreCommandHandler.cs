using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class UnbindPosCompanyStoreCommandHandler : ICommandHandler<UnbindPosCompanyStoreCommand, UnbindPosCompanyStoreResponse>
{
    private readonly IPosManagementService _posManagementService;

    public UnbindPosCompanyStoreCommandHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }
    
    public async Task<UnbindPosCompanyStoreResponse> Handle(IReceiveContext<UnbindPosCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.UnbindPosCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}