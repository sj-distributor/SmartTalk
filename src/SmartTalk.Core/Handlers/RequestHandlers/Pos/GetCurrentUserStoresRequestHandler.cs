using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetCurrentUserStoresRequestHandler : IRequestHandler<GetCurrentUserStoresRequest, GetCurrentUserStoresResponse>
{
    private readonly IPosService _posService;

    public GetCurrentUserStoresRequestHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task<GetCurrentUserStoresResponse> Handle(IReceiveContext<GetCurrentUserStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetCurrentUserStoresAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}