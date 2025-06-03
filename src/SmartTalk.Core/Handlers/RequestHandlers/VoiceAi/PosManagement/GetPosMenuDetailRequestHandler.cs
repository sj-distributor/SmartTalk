using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.RequestHandlers.VoiceAi.PosManagement;

public class GetPosMenuDetailRequestHandler : IRequestHandler<GetPosMenuDetailRequest, GetPosMenuDetailResponse>
{
    private readonly IPosManagementService _managementService;

    public GetPosMenuDetailRequestHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }

    public async Task<GetPosMenuDetailResponse> Handle(IReceiveContext<GetPosMenuDetailRequest> context, CancellationToken cancellationToken)
    {
        return await _managementService.GetPosMenuDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}