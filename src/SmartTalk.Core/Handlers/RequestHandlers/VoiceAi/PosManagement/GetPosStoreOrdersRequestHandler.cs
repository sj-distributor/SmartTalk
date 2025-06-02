using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.RequestHandlers.VoiceAi.PosManagement;

public class GetPosStoreOrdersRequestHandler : IRequestHandler<GetPosStoreOrdersRequest, GetPosStoreOrdersResponse>
{
    private readonly IPosManagementService _posManagementService;

    public GetPosStoreOrdersRequestHandler(IPosManagementService posManagementService)
    {
        _posManagementService = posManagementService;
    }

    public async Task<GetPosStoreOrdersResponse> Handle(IReceiveContext<GetPosStoreOrdersRequest> context, CancellationToken cancellationToken)
    {
        return await _posManagementService.GetPosStoreOrdersAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}