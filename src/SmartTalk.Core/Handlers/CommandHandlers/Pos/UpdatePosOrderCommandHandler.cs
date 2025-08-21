using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdatePosOrderCommandHandler : ICommandHandler<UpdatePosOrderCommand>
{
    private readonly IPosService _posService;

    public UpdatePosOrderCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task Handle(IReceiveContext<UpdatePosOrderCommand> context, CancellationToken cancellationToken)
    {
        await _posService.UpdatePosOrderAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}