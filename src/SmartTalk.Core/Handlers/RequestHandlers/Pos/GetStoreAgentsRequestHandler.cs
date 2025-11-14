using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetStoreAgentsRequestHandler : IRequestHandler<GetStoresAgentsRequest, GetStoresAgentsResponse>
{
    private readonly IPosService _posService;

    public GetStoreAgentsRequestHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task<GetStoresAgentsResponse> Handle(IReceiveContext<GetStoresAgentsRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetStoresAgentsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}