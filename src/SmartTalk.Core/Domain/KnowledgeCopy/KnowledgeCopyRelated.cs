using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.KnowledgeCopy;


[Table("knowledge_copy_related")]
public class KnowledgeCopyRelated : IEntity
{
    [Column("id")]
    public int Id { get; set; }
    
    [Column("source_knowledge_id")]
    public int SourceKnowledgeId { get; set; }

    [Column("target_knowledge_id")]
    public int TargetKnowledgeId { get; set; }
    
    [Column("copy_knowledge_points")]
    public string CopyKnowledgePoints { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}
