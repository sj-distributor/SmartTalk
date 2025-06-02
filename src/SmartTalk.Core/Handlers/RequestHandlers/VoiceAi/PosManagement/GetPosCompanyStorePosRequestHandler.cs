using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.RequestHandlers.VoiceAi.PosManagement;

public class GetPosCompanyStorePosRequestHandler : IRequestHandler<GetCompanyStorePosRequest, GetCompanyStorePosResponse>
{
    private readonly IPosManagementService _managementService;

    public GetPosCompanyStorePosRequestHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }
    
    public async Task<GetCompanyStorePosResponse> Handle(IReceiveContext<GetCompanyStorePosRequest> context, CancellationToken cancellationToken)
    {
        return await _managementService.GetCompanyStorePosAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}