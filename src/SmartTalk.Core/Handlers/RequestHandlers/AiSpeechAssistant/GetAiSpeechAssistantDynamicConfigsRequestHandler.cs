using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetAiSpeechAssistantDynamicConfigsRequestHandler : IRequestHandler<GetAiSpeechAssistantDynamicConfigsRequest, GetAiSpeechAssistantDynamicConfigsResponse>
{
    private readonly IAiSpeechAssistantService _service;

    public GetAiSpeechAssistantDynamicConfigsRequestHandler(IAiSpeechAssistantService service)
    {
        _service = service;
    }

    public async Task<GetAiSpeechAssistantDynamicConfigsResponse> Handle(IReceiveContext<GetAiSpeechAssistantDynamicConfigsRequest> context,
        CancellationToken cancellationToken)
    {
        return await _service.GetAiSpeechAssistantDynamicConfigsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
