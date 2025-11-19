using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetStructuredStoresRequestHandler : IRequestHandler<GetStructuredStoresRequest, GetStructuredStoresResponse>
{
    private readonly IPosService _posService;

    public GetStructuredStoresRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetStructuredStoresResponse> Handle(IReceiveContext<GetStructuredStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetStructuredStoresAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}