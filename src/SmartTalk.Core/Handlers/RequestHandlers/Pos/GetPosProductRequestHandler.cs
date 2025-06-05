using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosProductRequestHandler : IRequestHandler<GetPosProductRequest, GetPosProductResponse>
{
    private readonly IPosService _service;

    public GetPosProductRequestHandler(IPosService service)
    {
        _service = service;
    }
    
    public async Task<GetPosProductResponse> Handle(IReceiveContext<GetPosProductRequest> context, CancellationToken cancellationToken)
    {
        return await _service.GetPosProductAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}