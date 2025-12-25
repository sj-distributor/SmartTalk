namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantKnowledgeDto
{
    public int Id { get; set; }
    
    public int AssistantId { get; set; }
    
    public string Json { get; set; }
    
    public string Prompt { get; set; }
    
    public string Version { get; set; }
    
    public bool IsActive { get; set; }
    
    public string Brief { get; set; }
    
    public string Greetings { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public AiSpeechAssistantPremiseDto Premise { get; set; }
    
    public int CreatedBy { get; set; }
    
    public List<KnowledgeCopyRelatedDto> KnowledgeCopyRelated { get; set; }
}