using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.KnowledgeScenario;

[Table("knowledge_scene_folder")]
public class KnowledgeSceneFolder : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("name"), StringLength(128)]
    public string Name { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}
