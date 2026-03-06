using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_knowledge_detail")] 
public class AiSpeechAssistantKnowledgeDetail : IEntity, IHasModifiedFields 
{ 
    [Key] 
    [Column("id")] 
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)] 
    public int Id { get; set; } 
    
    [Column("knowledge_id")] 
    public int KnowledgeId { get; set; } 
    
    [Column("knowledge_name")] 
    public string KnowledgeName { get; set; } 
    
    [Column("format_type")] 
    public AiSpeechAssistantKonwledgeFormatType FormatType { get; set; } 
    
    [Column("content")]
    public string Content { get; set; }
    
    [Column("created_date")] 
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now; 
    
    [Column("lastModified_by")]
    public int? LastModifiedBy { get; set; } 
    
    [Column("lastModified_date")] 
    public DateTimeOffset? LastModifiedDate { get; set; } 
}