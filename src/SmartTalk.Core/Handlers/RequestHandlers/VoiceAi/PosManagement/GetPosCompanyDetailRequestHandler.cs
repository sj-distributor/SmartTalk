using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.RequestHandlers.VoiceAi.PosManagement;

public class GetPosCompanyDetailRequestHandler : IRequestHandler<GetPosCompanyDetailRequest, GetPosCompanyDetailResponse>
{
    private readonly IPosManagementService _managementService;

    public GetPosCompanyDetailRequestHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }

    public async Task<GetPosCompanyDetailResponse> Handle(IReceiveContext<GetPosCompanyDetailRequest> context, CancellationToken cancellationToken)
    {
        return await _managementService.GetPosCompanyDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}