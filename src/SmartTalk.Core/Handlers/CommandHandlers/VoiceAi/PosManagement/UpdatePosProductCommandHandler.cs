using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class UpdatePosProductCommandHandler : ICommandHandler<UpdatePosProductCommand, UpdatePosProductResponse>
{
    private readonly IPosManagementService _managementService;

    public UpdatePosProductCommandHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }

    public async Task<UpdatePosProductResponse> Handle(IReceiveContext<UpdatePosProductCommand> context, CancellationToken cancellationToken)
    {
        return await _managementService.UpdatePosProductAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}