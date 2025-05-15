using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.RequestHandlers.VoiceAi.PosManagement;

public class GePosCompanyStoreDetailRequestHandler : IRequestHandler<GetPosCompanyStoreDetailRequest, GetPosCompanyStoreDetailResponse>
{
    private readonly IPosManagementService _posManagementService;

    public GePosCompanyStoreDetailRequestHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }

    public async Task<GetPosCompanyStoreDetailResponse> Handle(IReceiveContext<GetPosCompanyStoreDetailRequest> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.GetPosCompanyStoreDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}