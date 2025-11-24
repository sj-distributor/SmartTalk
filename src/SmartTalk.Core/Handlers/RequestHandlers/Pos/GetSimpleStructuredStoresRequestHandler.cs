using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetSimpleStructuredStoresRequestHandler : IRequestHandler<GetSimpleStructuredStoresRequest, GetSimpleStructuredStoresResponse>
{
    private readonly IPosService _posService;

    public GetSimpleStructuredStoresRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetSimpleStructuredStoresResponse> Handle(IReceiveContext<GetSimpleStructuredStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetSimpleStructuredStoresAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}