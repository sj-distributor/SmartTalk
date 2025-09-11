using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetCompanyStoresRequestHandler : IRequestHandler<GetStoresRequest, GetPosStoresResponse>
{
    private readonly IPosService _posService;

    public GetCompanyStoresRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetPosStoresResponse> Handle(IReceiveContext<GetStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetStoresAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}