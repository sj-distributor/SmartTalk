using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosProductsRequestHandler : IRequestHandler<GetPosProductsRequest, GetPosProductsResponse>
{
    private readonly IPosService _posService;

    public GetPosProductsRequestHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task<GetPosProductsResponse> Handle(IReceiveContext<GetPosProductsRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetPosProductsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}