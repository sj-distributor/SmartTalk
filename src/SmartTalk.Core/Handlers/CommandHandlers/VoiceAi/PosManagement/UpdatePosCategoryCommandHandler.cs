using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class UpdatePosCategoryCommandHandler : ICommandHandler<UpdatePosCategoryCommand, UpdatePosCategoryResponse>
{
    private readonly IPosManagementService _managementService;

    public UpdatePosCategoryCommandHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }

    public async Task<UpdatePosCategoryResponse> Handle(IReceiveContext<UpdatePosCategoryCommand> context, CancellationToken cancellationToken)
    {
        return await _managementService.UpdatePosCategoryAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}