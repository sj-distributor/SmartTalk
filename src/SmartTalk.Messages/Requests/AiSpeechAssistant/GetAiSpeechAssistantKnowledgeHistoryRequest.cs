using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeHistoryRequest : IRequest
{
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
    
    public string Version { get; set; }
    
    public int AssistantId { get; set; }
}

public class GetAiSpeechAssistantKnowledgeHistoryResponse : SmartiesResponse<GetAiSpeechAssistantKnowledgeHistoryResponseData>
{
}

public class GetAiSpeechAssistantKnowledgeHistoryResponseData
{
    public int Count { get; set; }
    
    public List<AiSpeechAssistantKnowledgeDto> Knowledges { get; set; }
}