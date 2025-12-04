using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeVariableCacheRequest : IRequest
{
    public string CacheKey { get; set; }
    
    public string Filter { get; set; }
}