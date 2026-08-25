using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Core.Domain.KnowledgeScenario;

[Table("knowledge_scene_history_item")]
public class KnowledgeSceneHistoryItem : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("history_id")]
    public int HistoryId { get; set; }

    [Column("scene_item_id")]
    public int? SceneItemId { get; set; }

    [Column("name"), StringLength(128)]
    public string Name { get; set; }

    [Column("type")]
    public KnowledgeSceneItemType Type { get; set; } = KnowledgeSceneItemType.Text;

    [Column("content")]
    public string Content { get; set; }

    [Column("file_name"), StringLength(255)]
    public string FileName { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}
