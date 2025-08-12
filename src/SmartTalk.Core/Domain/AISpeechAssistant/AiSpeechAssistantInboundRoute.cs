using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.System;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_inbound_route")]
public class AiSpeechAssistantInboundRoute : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("from"), StringLength(48)]
    public string From { get; set; }

    [Column("to"), StringLength(48)]
    public string To { get; set; }
    
    [Column("timezone")]
    public string TimeZone { get; set; } = "Pacific Standard Time";
    
    [Column("start_time")]
    public TimeSpan? StartTime { get; set; }
    
    [Column("end_time")]
    public TimeSpan? EndTime { get; set; }
    
    [Column("is_full_day")]
    public bool IsFullDay { get; set; }
    
    [Column("dow_set")]
    public DayOfWeekSet DowSet { get; set; }

    [Column("forward_assistant_id")]
    public int? ForwardAssistantId { get; set; }
    
    [Column("forward_number")]
    public string ForwardNumber { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}
