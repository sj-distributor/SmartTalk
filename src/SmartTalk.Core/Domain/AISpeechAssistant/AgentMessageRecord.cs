using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("agent_message_record")]
public class AgentMessageRecord : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("agent_id")]
    public int AgentId { get; set; }

    [Column("record_id")]
    public int RecordId { get; set; }

    [Column("message_date")]
    public DateTimeOffset MessageDate { get; set; }

    [Column("message_number")]
    public int MessageNumber { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
    
    [Column("last_modified_date")]
    public DateTimeOffset LastModifiedDate { get; set; } = DateTimeOffset.Now;
}