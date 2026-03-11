using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetCurrentCompanyDynamicConfigsRequestHandler : IRequestHandler<GetCurrentCompanyDynamicConfigsRequest, GetCurrentCompanyDynamicConfigsResponse>
{
    private readonly IAiSpeechAssistantService _service;

    public GetCurrentCompanyDynamicConfigsRequestHandler(IAiSpeechAssistantService service)
    {
        _service = service;
    }

    public async Task<GetCurrentCompanyDynamicConfigsResponse> Handle(IReceiveContext<GetCurrentCompanyDynamicConfigsRequest> context,
        CancellationToken cancellationToken)
    {
        return await _service.GetCurrentCompanyDynamicConfigsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
