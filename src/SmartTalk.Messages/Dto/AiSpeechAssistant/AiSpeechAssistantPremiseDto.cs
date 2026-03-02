namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantPremiseDto
{
    public int Id { get; set; }
    
    public int AssistantId { get; set; }
    
    public string Content { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}