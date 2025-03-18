using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeRequest : IRequest
{
    public int AssistantId { get; set; }
    
    public int? KnowledgeId { get; set; }
}

public class GetAiSpeechAssistantKnowledgeResponse : SmartiesResponse<AiSpeechAssistantKnowledgeDto>
{
}