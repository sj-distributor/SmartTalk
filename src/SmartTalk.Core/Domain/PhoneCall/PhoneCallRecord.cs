using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Core.Domain.PhoneCall;

[Table("phone_call_record")]
public class PhoneCallRecord : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("session_id")]
    public string SessionId { get; set; }

    [Column("status")]
    public PhoneOrderRecordStatus Status { get; set; } = PhoneOrderRecordStatus.Recieved;
    
    [Column("restaurant")]
    public PhoneOrderRestaurant Restaurant { get; set; }
    
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
    
    [Column("call_status")]
    public PhoneCallStatus CallStatus { get; set; }
    
    [Column("agent")]
    public PhoneCallAgent Agent { get; set; }
    
    [NotMapped]
    public UserAccount UserAccount { get; set; }
    
    [Column("last_modified_by_name")]
    public string LastModifiedByName { get; set; }
}