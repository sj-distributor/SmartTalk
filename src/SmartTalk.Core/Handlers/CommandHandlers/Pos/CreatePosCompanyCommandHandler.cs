using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class CreatePosCompanyCommandHandler : ICommandHandler<CreateCompanyCommand, CreatePosCompanyResponse>
{
    private readonly IPosService _service;

    public CreatePosCompanyCommandHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<CreatePosCompanyResponse> Handle(IReceiveContext<CreateCompanyCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _service.CreatePosCompanyAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new CreatePosCompanyResponse
        {
            Data = @event.Company
        };
    }
}