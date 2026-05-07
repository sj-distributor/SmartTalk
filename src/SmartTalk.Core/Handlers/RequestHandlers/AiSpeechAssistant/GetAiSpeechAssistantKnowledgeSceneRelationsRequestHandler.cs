using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeSceneRelationsRequestHandler : IRequestHandler<GetAiSpeechAssistantKnowledgeSceneRelationsRequest, GetAiSpeechAssistantKnowledgeSceneRelationsResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public GetAiSpeechAssistantKnowledgeSceneRelationsRequestHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<GetAiSpeechAssistantKnowledgeSceneRelationsResponse> Handle(IReceiveContext<GetAiSpeechAssistantKnowledgeSceneRelationsRequest> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.GetAiSpeechAssistantKnowledgeSceneRelationsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
