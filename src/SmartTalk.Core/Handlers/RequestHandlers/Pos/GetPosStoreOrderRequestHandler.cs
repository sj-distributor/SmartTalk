using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosStoreOrderRequestHandler : IRequestHandler<GetPosStoreOrderRequest, GetPosStoreOrderResponse>
{
    private readonly IPosService _posService;

    public GetPosStoreOrderRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetPosStoreOrderResponse> Handle(IReceiveContext<GetPosStoreOrderRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetPosStoreOrderAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}