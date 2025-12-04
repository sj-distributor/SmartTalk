namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantKnowledgeVariableCacheDto
{
    public int Id { get; set; }

    public string CacheKey { get; set; }

    public string CacheValue { get; set; }
    
    public string Filter { get; set; }

    public DateTimeOffset LastUpdated { get; set; }
}