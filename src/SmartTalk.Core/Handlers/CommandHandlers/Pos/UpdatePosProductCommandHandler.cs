using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdatePosProductCommandHandler : ICommandHandler<UpdatePosProductCommand, UpdatePosProductResponse>
{
    private readonly IPosService _service;

    public UpdatePosProductCommandHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<UpdatePosProductResponse> Handle(IReceiveContext<UpdatePosProductCommand> context, CancellationToken cancellationToken)
    {
        return await _service.UpdatePosProductAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}