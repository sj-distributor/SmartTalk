using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeCapabilitiesRequestHandler
    : IRequestHandler<GetAiSpeechAssistantKnowledgeCapabilitiesRequest, GetAiSpeechAssistantKnowledgeCapabilitiesResponse>
{
    private readonly IAiSpeechAssistantService _assistantService;

    public GetAiSpeechAssistantKnowledgeCapabilitiesRequestHandler(IAiSpeechAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    public async Task<GetAiSpeechAssistantKnowledgeCapabilitiesResponse> Handle(
        IReceiveContext<GetAiSpeechAssistantKnowledgeCapabilitiesRequest> context,
        CancellationToken cancellationToken)
    {
        return await _assistantService
            .GetAiSpeechAssistantKnowledgeCapabilitiesAsync(context.Message, cancellationToken)
            .ConfigureAwait(false);
    }
}
