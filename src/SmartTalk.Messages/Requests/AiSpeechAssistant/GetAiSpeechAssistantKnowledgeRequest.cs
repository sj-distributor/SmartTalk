using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeRequest : IRequest
{
    public int AssistantId { get; set; }
    
    public int? KnowledgeId { get; set; }
}

public class GetAiSpeechAssistantKnowledgeResponse : SmartTalkResponse<AiSpeechAssistantKnowledgeDto>
{
}