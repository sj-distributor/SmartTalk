using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeHistoryRequestHandler : IRequestHandler<GetAiSpeechAssistantKnowledgeHistoryRequest, GetAiSpeechAssistantKnowledgeHistoryResponse>
{
    private readonly IAiSpeechAssistantService _assistantService;

    public GetAiSpeechAssistantKnowledgeHistoryRequestHandler(IAiSpeechAssistantService assistantService)
    {
        _assistantService = assistantService;
    }
    
    public async Task<GetAiSpeechAssistantKnowledgeHistoryResponse> Handle(IReceiveContext<GetAiSpeechAssistantKnowledgeHistoryRequest> context, CancellationToken cancellationToken)
    {
        return await _assistantService.GetAiSpeechAssistantKnowledgeHistoryAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}