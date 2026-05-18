using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Core.Domain.KnowledgeScenario;

[Table("knowledge_scene_history")]
public class KnowledgeSceneHistory : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("scene_id")]
    public int SceneId { get; set; }

    [Column("folder_id")]
    public int FolderId { get; set; }

    [Column("name"), StringLength(128)]
    public string Name { get; set; }

    [Column("description"), StringLength(2048)]
    public string Description { get; set; }

    [Column("version"), StringLength(128)]
    public string Version { get; set; }

    [Column("brief"), StringLength(128)]
    public string Brief { get; set; }

    [Column("status")]
    public KnowledgeSceneStatus Status { get; set; }

    [Column("is_active", TypeName = "tinyint(1)")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [Column("snapshot_at")]
    public DateTimeOffset SnapshotAt { get; set; } = DateTimeOffset.UtcNow;
    
    [NotMapped]
    public List<KnowledgeSceneItem> SceneItems { get; set; }
}
