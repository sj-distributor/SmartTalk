using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeRequestHandler : IRequestHandler<GetAiSpeechAssistantKnowledgeRequest, GetAiSpeechAssistantKnowledgeResponse>
{
    private readonly IAiSpeechAssistantService _assistantService;

    public GetAiSpeechAssistantKnowledgeRequestHandler(IAiSpeechAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    public async Task<GetAiSpeechAssistantKnowledgeResponse> Handle(IReceiveContext<GetAiSpeechAssistantKnowledgeRequest> context, CancellationToken cancellationToken)
    {
        return await _assistantService.GetAiSpeechAssistantKnowledgeAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}