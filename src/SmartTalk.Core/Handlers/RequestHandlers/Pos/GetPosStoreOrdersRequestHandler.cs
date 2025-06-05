using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosStoreOrdersRequestHandler : IRequestHandler<GetPosStoreOrdersRequest, GetPosStoreOrdersResponse>
{
    private readonly IPosService _posService;

    public GetPosStoreOrdersRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetPosStoreOrdersResponse> Handle(IReceiveContext<GetPosStoreOrdersRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetPosStoreOrdersAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}