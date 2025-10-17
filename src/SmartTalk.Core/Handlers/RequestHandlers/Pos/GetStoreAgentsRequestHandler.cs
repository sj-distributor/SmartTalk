using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetStoreAgentsRequestHandler : IRequestHandler<GetAgentsStoresRequest, GetAgentsStoresResponse>
{
    private readonly IPosService _posService;

    public GetStoreAgentsRequestHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task<GetAgentsStoresResponse> Handle(IReceiveContext<GetAgentsStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetAgentsStoresAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}