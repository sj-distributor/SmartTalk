using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class DeletePosCompanyCommandHandler : ICommandHandler<DeletePosCompanyCommand, DeletePosCompanyResponse>
{
    private readonly IPosManagementService _managementService;

    public DeletePosCompanyCommandHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }

    public async Task<DeletePosCompanyResponse> Handle(IReceiveContext<DeletePosCompanyCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _managementService.DeletePosCompanyAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new DeletePosCompanyResponse
        {
            Data = @event.Company
        };
    }
}