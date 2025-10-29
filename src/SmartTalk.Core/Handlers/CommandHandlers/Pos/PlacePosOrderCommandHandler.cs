using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class PlacePosOrderCommandHandler :ICommandHandler<PlacePosOrderCommand, PlacePosOrderResponse>
{
    private readonly IPosService _posService;

    public PlacePosOrderCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<PlacePosOrderResponse> Handle(IReceiveContext<PlacePosOrderCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _posService.PlacePosStoreOrdersAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);
        
        return new PlacePosOrderResponse
        {
            Data = @event.Order
        };
    }
}