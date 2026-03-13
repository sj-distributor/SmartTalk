using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetOrderReservationInfoRequestHandler : IRequestHandler<GetOrderReservationInfoRequest, GetOrderReservationInfoResponse>
{
    private readonly IPosService _posService;

    public GetOrderReservationInfoRequestHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task<GetOrderReservationInfoResponse> Handle(IReceiveContext<GetOrderReservationInfoRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetOrderReservationInfoAsync(context.Message, cancellationToken: cancellationToken).ConfigureAwait(false); 
    }
}