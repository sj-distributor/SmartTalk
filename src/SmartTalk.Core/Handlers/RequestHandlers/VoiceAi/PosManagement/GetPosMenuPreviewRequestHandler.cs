using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.RequestHandlers.VoiceAi.PosManagement;

public class GetPosMenuPreviewRequestHandler : IRequestHandler<GetPosMenuPreviewRequest, GetPosMenuPreviewResponse>
{
    private readonly IPosManagementService _managementService;

    public GetPosMenuPreviewRequestHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }

    public async Task<GetPosMenuPreviewResponse> Handle(IReceiveContext<GetPosMenuPreviewRequest> context, CancellationToken cancellationToken)
    {
        return await _managementService.GetPosMenuPreviewAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}