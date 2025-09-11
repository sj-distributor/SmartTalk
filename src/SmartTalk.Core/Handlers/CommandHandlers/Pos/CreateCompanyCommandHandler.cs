using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class CreateCompanyCommandHandler : ICommandHandler<CreateCompanyCommand, CreateCompanyResponse>
{
    private readonly IPosService _service;

    public CreateCompanyCommandHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<CreateCompanyResponse> Handle(IReceiveContext<CreateCompanyCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _service.CreatePosCompanyAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new CreateCompanyResponse
        {
            Data = @event.Company
        };
    }
}