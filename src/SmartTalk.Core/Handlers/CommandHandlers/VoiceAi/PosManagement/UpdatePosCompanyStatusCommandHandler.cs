using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.CommandHandlers.VoiceAi.PosManagement;

public class UpdatePosCompanyStatusCommandHandler: ICommandHandler<UpdatePosCompanyStatusCommand, UpdatePosCompanyStatusResponse>
{
    private readonly IPosManagementService _managementService;

    public UpdatePosCompanyStatusCommandHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }

    public async Task<UpdatePosCompanyStatusResponse> Handle(IReceiveContext<UpdatePosCompanyStatusCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _managementService.UpdatePosCompanyStatusAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new UpdatePosCompanyStatusResponse
        {
            Data = @event.Company
        };
    }
}