using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Core.Domain.KnowledgeScenario;

[Table("knowledge_scene")]
public class KnowledgeScene : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("folder_id")]
    public int FolderId { get; set; }

    [Column("name"), StringLength(128)]
    public string Name { get; set; }

    [Column("description"), StringLength(2048)]
    public string Description { get; set; }

    [Column("status")]
    public KnowledgeSceneStatus Status { get; set; } = KnowledgeSceneStatus.OffShelf;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}
