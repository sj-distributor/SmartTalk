using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class UpdatePosCompanyCommandHandler: ICommandHandler<UpdatePosCompanyCommand, UpdatePosCompanyResponse>
{
    private readonly IPosManagementService _managementService;

    public UpdatePosCompanyCommandHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }

    public async Task<UpdatePosCompanyResponse> Handle(IReceiveContext<UpdatePosCompanyCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _managementService.UpdatePosCompanyAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new UpdatePosCompanyResponse
        {
            Data = @event.Company
        };
    }
}