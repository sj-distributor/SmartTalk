using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_session")]
public class AiSpeechAssistantSession : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("assistant_id")]
    public int AssistantId { get; set; }

    [Column("session_id", TypeName = "char(36)")]
    public Guid SessionId { get; set; }

    [Column("count")]
    public int Count { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}