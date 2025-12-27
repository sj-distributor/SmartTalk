using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_timer")]
public class AiSpeechAssistantTimer : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("assistant_id")]
    public int AssistantId { get; set; }
    
    [Column("time_span_seconds")]
    public int TimeSpanSeconds { get; set; }
    
    [Column("alter_content")]
    public string AlterContent { get; set; }
    
    [Column("skip_round")]
    public int? SkipRound { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}