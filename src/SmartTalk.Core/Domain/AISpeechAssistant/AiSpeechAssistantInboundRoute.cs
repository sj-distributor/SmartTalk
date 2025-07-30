using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_inbound_route")]
public class AiSpeechAssistantInboundRoute : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("assistant_id")]
    public int AssistantId { get; set; }

    [Column("from"), StringLength(48)]
    public string From { get; set; }

    [Column("to"), StringLength(48)]
    public string To { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}
