using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class PlacePosOrderCommandHandler :ICommandHandler<PlacePosOrderCommand, PlacePosOrderResponse>
{
    private readonly IPosManagementService _posManagementService;

    public PlacePosOrderCommandHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }

    public async Task<PlacePosOrderResponse> Handle(IReceiveContext<PlacePosOrderCommand> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.PlacePosStoreOrdersAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}