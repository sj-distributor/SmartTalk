using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_knowledge_scene_relation")]
public class AiSpeechAssistantKnowledgeSceneRelation : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("knowledge_id")]
    public int KnowledgeId { get; set; }

    [Column("scene_id")]
    public int SceneId { get; set; }

    [Column("source_type")]
    public string SourceTypeValue
    {
        get => SourceType.ToString();
        set
        {
            if (Enum.TryParse<AiSpeechAssistantKnowledgeSceneRelationSourceType>(value, true, out var parsed))
            {
                SourceType = parsed;
                return;
            }

            SourceType = AiSpeechAssistantKnowledgeSceneRelationSourceType.Manual;
        }
    }

    [NotMapped]
    public AiSpeechAssistantKnowledgeSceneRelationSourceType SourceType { get; set; } = AiSpeechAssistantKnowledgeSceneRelationSourceType.Manual;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
