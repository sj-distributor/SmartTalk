using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Core.Domain.KnowledgeScenario;

[Table("knowledge_scene_knowledge")]
public class KnowledgeSceneKnowledge : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("scene_id")]
    public int SceneId { get; set; }

    [Column("name"), StringLength(128)]
    public string Name { get; set; }

    [Column("type")]
    public KnowledgeSceneKnowledgeType Type { get; set; } = KnowledgeSceneKnowledgeType.Text;

    [Column("content")]
    public string Content { get; set; }

    [Column("file_name"), StringLength(255)]
    public string FileName { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}
