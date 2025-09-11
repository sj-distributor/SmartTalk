using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdateCompanyStatusCommandHandler: ICommandHandler<UpdateCompanyStatusCommand, UpdateCompanyStatusResponse>
{
    private readonly IPosService _service;

    public UpdateCompanyStatusCommandHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<UpdateCompanyStatusResponse> Handle(IReceiveContext<UpdateCompanyStatusCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _service.UpdatePosCompanyStatusAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new UpdateCompanyStatusResponse
        {
            Data = @event.Company
        };
    }
}