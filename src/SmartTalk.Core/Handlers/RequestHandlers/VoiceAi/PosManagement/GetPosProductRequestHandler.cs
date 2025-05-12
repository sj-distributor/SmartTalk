using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.RequestHandlers.VoiceAi.PosManagement;

public class GetPosProductRequestHandler : IRequestHandler<GetPosProductRequest, GetPosProductResponse>
{
    private readonly IPosManagementService _managementService;

    public GetPosProductRequestHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }
    
    public async Task<GetPosProductResponse> Handle(IReceiveContext<GetPosProductRequest> context, CancellationToken cancellationToken)
    {
        return await _managementService.GetPosProductAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}