using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.RequestHandlers.VoiceAi.PosManagement;

public class GetPosStoreUsersRequestHandler : IRequestHandler<GetPosStoreUsersRequest, GetPosStoreUsersResponse>
{
    private readonly IPosManagementService _managementService;

    public GetPosStoreUsersRequestHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }

    public async Task<GetPosStoreUsersResponse> Handle(IReceiveContext<GetPosStoreUsersRequest> context, CancellationToken cancellationToken)
    {
        return await _managementService.GetPosStoreUsersAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}