using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class DeleteCompanyCommandHandler : ICommandHandler<DeleteCompanyCommand, DeleteCompanyResponse>
{
    private readonly IPosService _service;

    public DeleteCompanyCommandHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<DeleteCompanyResponse> Handle(IReceiveContext<DeleteCompanyCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _service.DeletePosCompanyAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new DeleteCompanyResponse
        {
            Data = @event.Company
        };
    }
}