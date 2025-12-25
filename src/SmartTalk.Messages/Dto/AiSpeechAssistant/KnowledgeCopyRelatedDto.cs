namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class KnowledgeCopyRelatedDto
{
    public int Id { get; set; }
    
    public int AssistantId { get; set; }
    
    public int SourceKnowledgeId { get; set; }
    
    public int SourceKnowledgeName { get; set; }
    
    public int TargetKnowledgeId { get; set; }
    
    public string CopyKnowledgePoints { get; set; }

    public bool IsSyncUpdate { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
    
    public string RelatedFrom { get; set; }
}
