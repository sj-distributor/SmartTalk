using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class CheckAiSpeechAssistantDescriptionExistsRequestHandler : IRequestHandler<CheckAiSpeechAssistantDescriptionExistsRequest, CheckAiSpeechAssistantDescriptionExistsResponse>
{
    private readonly IAiSpeechAssistantService _service;

    public CheckAiSpeechAssistantDescriptionExistsRequestHandler(IAiSpeechAssistantService service)
    {
        _service = service;
    }

    public async Task<CheckAiSpeechAssistantDescriptionExistsResponse> Handle(IReceiveContext<CheckAiSpeechAssistantDescriptionExistsRequest> context, CancellationToken cancellationToken)
    {
        return await _service.CheckAiSpeechAssistantDescriptionExistsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
