using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_description")]
public class AiSpeechAssistantDescription : IEntity
{
    [Key]
    [Column("model_id"), StringLength(64)]
    public string ModelId { get; set; }

    [Column("model_description"), StringLength(1024)]
    public string ModelDescription { get; set; }

    [Column("model_value"), StringLength(512)]
    public string ModelValue { get; set; }
}
