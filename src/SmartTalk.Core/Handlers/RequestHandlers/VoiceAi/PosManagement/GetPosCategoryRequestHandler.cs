using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Handlers.RequestHandlers.VoiceAi.PosManagement;

public class GetPosCategoryRequestHandler : IRequestHandler<GetPosCategoryRequest, GetPosCategoryResponse>
{
    private readonly IPosManagementService _managementService;

    public GetPosCategoryRequestHandler(IPosManagementService managementService)
    {
        _managementService = managementService;
    }

    public async Task<GetPosCategoryResponse> Handle(IReceiveContext<GetPosCategoryRequest> context, CancellationToken cancellationToken)
    {
        return await _managementService.GetPosCategoryAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}