using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GePosCompanyStoreDetailRequestHandler : IRequestHandler<GetPosCompanyStoreDetailRequest, GetPosCompanyStoreDetailResponse>
{
    private readonly IPosService _posService;

    public GePosCompanyStoreDetailRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetPosCompanyStoreDetailResponse> Handle(IReceiveContext<GetPosCompanyStoreDetailRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetPosCompanyStoreDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}