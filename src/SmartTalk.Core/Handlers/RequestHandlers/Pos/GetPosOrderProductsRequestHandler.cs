using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosOrderProductsRequestHandler : IRequestHandler<GetPosOrderProductsRequest, GetPosOrderProductsResponse>
{
    private readonly IPosService _posService;

    public GetPosOrderProductsRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetPosOrderProductsResponse> Handle(IReceiveContext<GetPosOrderProductsRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetPosOrderProductsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}