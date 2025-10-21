using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdatePosCompanyStatusCommandHandler: ICommandHandler<UpdatePosCompanyStatusCommand, UpdatePosCompanyStatusResponse>
{
    private readonly IPosService _service;

    public UpdatePosCompanyStatusCommandHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<UpdatePosCompanyStatusResponse> Handle(IReceiveContext<UpdatePosCompanyStatusCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _service.UpdatePosCompanyStatusAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new UpdatePosCompanyStatusResponse
        {
            Data = @event.Company
        };
    }
}