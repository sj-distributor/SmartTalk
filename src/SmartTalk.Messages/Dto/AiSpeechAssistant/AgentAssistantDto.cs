namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AgentAssistantDto
{
    public int Id { get; set; }
    
    public int AgentId { get; set; }
    
    public int AssistantId { get; set; }
    
    public int? CreatedBy { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public int? LastModifiedBy { get; set; }
    
    public DateTimeOffset LastModifiedDate { get; set; }
}