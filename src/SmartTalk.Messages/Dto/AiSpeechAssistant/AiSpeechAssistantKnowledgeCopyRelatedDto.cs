namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantKnowledgeCopyRelatedDto
{
    public int Id { get; set; }
    
    public int SourceKnowledgeId { get; set; }
    
    public int TargetKnowledgeId { get; set; }
    
    public string CopyKnowledgePoints { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
    
    public bool IsSyncUpdate { get; set; }
    
    public string RelatedFrom { get; set; }
}
