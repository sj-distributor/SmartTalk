using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Sales;

namespace SmartTalk.Core.Domain.Sales;

[Table("phone_order_push_task")]
public class PhoneOrderPushTask : IEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("record_id")]
    public int RecordId { get; set; }
    
    [Column("parent_record_id")]
    public int? ParentRecordId { get; set; }
    
    [Column("assistant_id")]
    public int AssistantId { get; set; }
    
    [Column("business_key"), StringLength(128)]
    public string BusinessKey { get; set; }
    
    [Column("task_type")]
    public PhoneOrderPushTaskType TaskType { get; set; }
    
    [Column("request_json")]
    public string RequestJson { get; set; }
    
    [Column("status")]
    public PhoneOrderPushTaskStatus Status { get; set; }
    
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
