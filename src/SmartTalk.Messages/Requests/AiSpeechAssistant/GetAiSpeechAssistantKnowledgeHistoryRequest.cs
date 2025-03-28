using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeHistoryRequest : IRequest
{
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
    
    public int AssistantId { get; set; }
}

public class GetAiSpeechAssistantKnowledgeHistoryResponse : SmartTalkResponse<GetAiSpeechAssistantKnowledgeHistoryResponseData>
{
}

public class GetAiSpeechAssistantKnowledgeHistoryResponseData
{
    public int Count { get; set; }
    
    public List<AiSpeechAssistantKnowledgeDto> Knowledges { get; set; }
}