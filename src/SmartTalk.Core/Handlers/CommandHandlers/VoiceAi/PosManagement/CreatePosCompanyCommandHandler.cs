using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class CreatePosCompanyCommandHandler : ICommandHandler<CreatePosCompanyCommand, CreatePosCompanyResponse>
{
    private readonly IPosManagementService _managementService;

    public CreatePosCompanyCommandHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }

    public async Task<CreatePosCompanyResponse> Handle(IReceiveContext<CreatePosCompanyCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _managementService.CreatePosCompanyAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new CreatePosCompanyResponse
        {
            Data = @event.Company
        };
    }
}