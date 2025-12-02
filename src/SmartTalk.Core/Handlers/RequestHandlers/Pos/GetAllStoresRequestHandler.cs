using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetAllStoresRequestHandler : IRequestHandler<GetAllStoresRequest, GetAllStoresResponse>
{
    private readonly IPosService _posService;

    public GetAllStoresRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetAllStoresResponse> Handle(IReceiveContext<GetAllStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetAllStoresAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}