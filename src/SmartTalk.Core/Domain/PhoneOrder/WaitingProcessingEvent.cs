using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.PhoneOrder;
using TaskStatus = SmartTalk.Messages.Enums.PhoneOrder.TaskStatus;

namespace SmartTalk.Core.Domain.PhoneOrder;

[Table("waiting_processing_event")]
public class WaitingProcessingEvent : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("record_id")]
    public int RecordId { get; set; }
    
    [Column("agent_id")]
    public int AgentId { get; set; }
    
    [Column("task_type")]
    public TaskType TaskType { get; set; }

    [Column("task_status")]
    public TaskStatus TaskStatus { get; set; }

    [Column("task_source")]
    public string TaskSource { get; set; }
    
    [Column("is_include_todo")]
    public bool IsIncludeTodo { get; set; }

    [Column("created_date")] 
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
    
    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }
    
    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}