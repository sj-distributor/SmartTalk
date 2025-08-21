using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdatePosCompanyCommandHandler: ICommandHandler<UpdatePosCompanyCommand, UpdatePosCompanyResponse>
{
    private readonly IPosService _service;

    public UpdatePosCompanyCommandHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<UpdatePosCompanyResponse> Handle(IReceiveContext<UpdatePosCompanyCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _service.UpdatePosCompanyAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new UpdatePosCompanyResponse
        {
            Data = @event.Company
        };
    }
}