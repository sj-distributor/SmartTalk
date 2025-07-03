using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Core.Domain.PhoneOrder;

[Table("phone_order_record")]
public class PhoneOrderRecord : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("agent_id")]
    public int AgentId { get; set; }
    
    [Column("session_id")]
    public string SessionId { get; set; }

    [Column("status")]
    public PhoneOrderRecordStatus Status { get; set; } = PhoneOrderRecordStatus.Recieved;
    
    [Column("tips")]
    public string Tips { get; set; }
    
    [Column("transcription_text")]
    public string TranscriptionText { get; set; }
    
    [Column("url")]
    public string Url { get; set; }
    
    [Column("language")]
    public TranscriptionLanguage Language { get; set; }
    
    [Column("manual_order_id")]
    public long? ManualOrderId { get; set; }
    
    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }
    
    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
    
    [Column("transcription_job_id")]
    public string TranscriptionJobId { get; set; }
    
    [Column("order_status")]
    public PhoneOrderOrderStatus OrderStatus { get; set; } = PhoneOrderOrderStatus.Pending;
    
    [Column("phone_number"), StringLength(50)]
    public string PhoneNumber { get; set; }
    
    [Column("customer_name"), StringLength(50)]
    public string CustomerName { get; set; }
    
    [Column("comments")]
    public string Comments { get; set; }
    
    [NotMapped]
    public UserAccount UserAccount { get; set; }
    
    [Column("last_modified_by_name")]
    public string LastModifiedByName { get; set; }
    
    [NotMapped]
    public Restaurant RestaurantInfo { get; set; }
}