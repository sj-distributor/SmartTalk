using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.RequestHandlers.VoiceAi.PosManagement;

public class GetPosCompanyStoresRequestHandler : IRequestHandler<GetPosStoresRequest, GetPosStoresResponse>
{
    private readonly IPosManagementService _posManagementService;

    public GetPosCompanyStoresRequestHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }

    public async Task<GetPosStoresResponse> Handle(IReceiveContext<GetPosStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.GetPosStoresAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}