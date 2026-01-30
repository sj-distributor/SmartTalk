namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantSessionDto
{
    public int Id { get; set; }
    
    public int AssistantId { get; set; }
    
    public Guid SessionId { get; set; }
    
    public int Count { get; set; }
    
    public AiSpeechAssistantPremiseDto Premise { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}