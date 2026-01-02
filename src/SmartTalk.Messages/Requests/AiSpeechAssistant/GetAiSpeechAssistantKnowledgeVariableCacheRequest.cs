using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeVariableCacheRequest : IRequest
{
    public string CacheKey { get; set; }
    
    public string Filter { get; set; }
}

public class GetAiSpeechAssistantKnowledgeVariableCacheResponse : SmartTalkResponse<GetAiSpeechAssistantKnowledgeVariableCacheData>;

public class GetAiSpeechAssistantKnowledgeVariableCacheData
{
    public List<AiSpeechAssistantKnowledgeVariableCacheDto> Caches { get; set; }
}