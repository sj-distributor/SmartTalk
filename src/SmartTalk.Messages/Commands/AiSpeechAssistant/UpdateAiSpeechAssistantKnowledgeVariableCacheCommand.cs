using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class UpdateAiSpeechAssistantKnowledgeVariableCacheCommand : ICommand
{
    public string CacheKey { get; set; }
    
    public string CacheValue { get; set; }
    
    public string Filter { get; set; }
}