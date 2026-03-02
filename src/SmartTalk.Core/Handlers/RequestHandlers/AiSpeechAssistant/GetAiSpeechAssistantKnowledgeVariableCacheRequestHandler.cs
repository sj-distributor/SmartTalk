using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeVariableCacheRequestHandler : IRequestHandler<GetAiSpeechAssistantKnowledgeVariableCacheRequest, GetAiSpeechAssistantKnowledgeVariableCacheResponse>
{
    private readonly IAiSpeechAssistantService _aiiSpeechAssistantService;

    public GetAiSpeechAssistantKnowledgeVariableCacheRequestHandler(IAiSpeechAssistantService aiiSpeechAssistantService)
    {
        _aiiSpeechAssistantService = aiiSpeechAssistantService;
    }

    public async Task<GetAiSpeechAssistantKnowledgeVariableCacheResponse> Handle(IReceiveContext<GetAiSpeechAssistantKnowledgeVariableCacheRequest> context, CancellationToken cancellationToken)
    {
        return await _aiiSpeechAssistantService.GetAiSpeechAssistantKnowledgeVariableCacheAsync(context.Message, cancellationToken);
    }
}