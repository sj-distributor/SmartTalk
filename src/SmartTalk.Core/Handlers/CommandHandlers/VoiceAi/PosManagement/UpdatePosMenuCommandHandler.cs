using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class UpdatePosMenuCommandHandler : ICommandHandler<UpdatePosMenuCommand, UpdatePosMenuResponse>
{
    private readonly IPosManagementService _managementService;

    public UpdatePosMenuCommandHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }

    public async Task<UpdatePosMenuResponse> Handle(IReceiveContext<UpdatePosMenuCommand> context, CancellationToken cancellationToken)
    {
        return await _managementService.UpdatePosMenuAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}