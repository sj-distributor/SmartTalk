using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.KnowledgeScenario;

[Table("knowledge_scene_company")]
public class KnowledgeSceneCompany : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("scene_id")]
    public int SceneId { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("store_id")]
    public int? StoreId { get; set; }

    [Column("is_applied", TypeName = "tinyint(1)")]
    public bool IsApplied { get; set; }

    [Column("authorized_at")]
    public DateTimeOffset AuthorizedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("applied_at")]
    public DateTimeOffset? AppliedAt { get; set; }
}
