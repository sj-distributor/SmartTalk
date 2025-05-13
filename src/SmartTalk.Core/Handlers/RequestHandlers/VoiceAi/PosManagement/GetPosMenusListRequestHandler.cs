using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.RequestHandlers.VoiceAi.PosManagement;

public class GetPosMenusListRequestHandler : IRequestHandler<GetPosMenusListRequest, GetPosMenusListResponse>
{
    private readonly IPosManagementService _managementService;

    public GetPosMenusListRequestHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }
    
    public async Task<GetPosMenusListResponse> Handle(IReceiveContext<GetPosMenusListRequest> context, CancellationToken cancellationToken)
    {
        return await _managementService.GetPosMenusListAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}