using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetStoreAgentsRequestHandler : IRequestHandler<GetStoreAgentsRequest, GetStoreAgentsResponse>
{
    private readonly IPosService _posService;

    public GetStoreAgentsRequestHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task<GetStoreAgentsResponse> Handle(IReceiveContext<GetStoreAgentsRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetStoreAgentsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}