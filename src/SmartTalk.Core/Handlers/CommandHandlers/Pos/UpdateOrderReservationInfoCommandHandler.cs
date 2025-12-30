using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdateOrderReservationInfoCommandHandler : ICommandHandler<UpdateOrderReservationInfoCommand, UpdateOrderReservationInfoResponse>
{
    private readonly IPosService _posService;

    public UpdateOrderReservationInfoCommandHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task<UpdateOrderReservationInfoResponse> Handle(IReceiveContext<UpdateOrderReservationInfoCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.UpdateOrderReservationInfoAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}