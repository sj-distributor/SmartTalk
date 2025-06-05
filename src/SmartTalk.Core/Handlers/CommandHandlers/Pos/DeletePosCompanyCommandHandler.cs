using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class DeletePosCompanyCommandHandler : ICommandHandler<DeletePosCompanyCommand, DeletePosCompanyResponse>
{
    private readonly IPosService _service;

    public DeletePosCompanyCommandHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<DeletePosCompanyResponse> Handle(IReceiveContext<DeletePosCompanyCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _service.DeletePosCompanyAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new DeletePosCompanyResponse
        {
            Data = @event.Company
        };
    }
}