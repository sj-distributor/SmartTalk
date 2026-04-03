using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_dynamic_config")]
public class AiSpeechAssistantDynamicConfig : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("name"), StringLength(128)]
    public string Name { get; set; }

    [Column("level")]
    public AiSpeechAssistantDynamicConfigLevel Level { get; set; }

    [Column("parent_id")]
    public int? ParentId { get; set; }

    [Column("status")]
    public bool Status { get; set; }
}
