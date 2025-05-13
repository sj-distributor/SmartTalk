using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class SyncPosConfigurationCommandHandler: ICommandHandler<SyncPosConfigurationCommand, SyncPosConfigurationResponse>
{
    private readonly IPosManagementService _posManagementService;

    public SyncPosConfigurationCommandHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }

    public async Task<SyncPosConfigurationResponse> Handle(IReceiveContext<SyncPosConfigurationCommand> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.SyncPosConfigurationAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}