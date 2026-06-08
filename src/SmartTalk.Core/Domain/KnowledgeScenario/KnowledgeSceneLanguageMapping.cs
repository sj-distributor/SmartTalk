using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.KnowledgeScenario;

[Table("knowledge_scene_language_mapping")]
public class KnowledgeSceneLanguageMapping : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("scene_id")]
    public int SceneId { get; set; }

    [Column("language"), StringLength(64)]
    public string Language { get; set; }

    [Column("is_active", TypeName = "tinyint(1)")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
