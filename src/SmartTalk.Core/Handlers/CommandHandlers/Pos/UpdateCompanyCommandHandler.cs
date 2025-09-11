using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdateCompanyCommandHandler: ICommandHandler<UpdateCompanyCommand, UpdateCompanyResponse>
{
    private readonly IPosService _service;

    public UpdateCompanyCommandHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<UpdateCompanyResponse> Handle(IReceiveContext<UpdateCompanyCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _service.UpdatePosCompanyAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new UpdateCompanyResponse
        {
            Data = @event.Company
        };
    }
}