using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.RequestHandlers.VoiceAi.PosManagement;

public class GetPosCompanyWithStoresRequestHandler : IRequestHandler<GetPosCompanyWithStoresRequest, GetPosCompanyWithStoresResponse>
{
    private readonly IPosManagementService _posManagementService;

    public GetPosCompanyWithStoresRequestHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }

    public async Task<GetPosCompanyWithStoresResponse> Handle(IReceiveContext<GetPosCompanyWithStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.GetPosCompanyWithStoresAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}