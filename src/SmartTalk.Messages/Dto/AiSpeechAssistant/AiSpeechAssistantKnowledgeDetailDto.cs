using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantKnowledgeDetailDto
{
    public int Id { get; set; } 
    
    public int KnowledgeId { get; set; } 
    
    public string KnowledgeName { get; set; } 
    
    public AiSpeechAssistantKonwledgeFormatType FormatType { get; set; } 
    
    public string Content { get; set; }

    public string FileName { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now; 
    
    public int? LastModifiedBy { get; set; } 
    
    public DateTimeOffset? LastModifiedDate { get; set; }

    public int? RelatedKnowledgeId { get; set; }

    public string RelatedFrom { get; set; }
}
