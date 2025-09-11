using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GeCompanyStoreDetailRequestHandler : IRequestHandler<GetCompanyStoreDetailRequest, GetCompanyStoreDetailResponse>
{
    private readonly IPosService _posService;

    public GeCompanyStoreDetailRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetCompanyStoreDetailResponse> Handle(IReceiveContext<GetCompanyStoreDetailRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetCompanyStoreDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}