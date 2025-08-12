using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosAgentsRequestHandler : IRequestHandler<GetPosCurrentUserStoresRequest, GetPosCurrentUserStoresResponse>
{
    private readonly IPosService _posService;

    public GetPosAgentsRequestHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task<GetPosCurrentUserStoresResponse> Handle(IReceiveContext<GetPosCurrentUserStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetPosAgentsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}