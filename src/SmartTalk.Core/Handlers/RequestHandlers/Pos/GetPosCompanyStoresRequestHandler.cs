using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosCompanyStoresRequestHandler : IRequestHandler<GetStoresRequest, GetPosStoresResponse>
{
    private readonly IPosService _posService;

    public GetPosCompanyStoresRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetPosStoresResponse> Handle(IReceiveContext<GetStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetPosStoresAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}