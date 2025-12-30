using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_knowledge_copy_related")]
public class AiSpeechAssistantKnowledgeCopyRelated : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("source_knowledge_id")]
    public int SourceKnowledgeId { get; set; }

    [Column("target_knowledge_id")]
    public int TargetKnowledgeId { get; set; }
    
    [Column("copy_knowledge_points")]
    public string CopyKnowledgePoints { get; set; }
    
    [Column("is_sync_update")]
    public bool IsSyncUpdate { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}
