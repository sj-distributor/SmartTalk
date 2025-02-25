using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantDto
{
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public string DidNumber { get; set; }
    
    public string Url { get; set; }
    
    public AiSpeechAssistantProvider Provider { get; set; }
    
    public int AgentId { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}