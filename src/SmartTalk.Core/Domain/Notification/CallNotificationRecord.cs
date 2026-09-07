using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Notification;

namespace SmartTalk.Core.Domain.Notification;

[Table("call_notification_record")]
public class CallNotificationRecord : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("phone_order_record_id")]
    public int PhoneOrderRecordId { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }

    [Column("agent_id")]
    public int AgentId { get; set; }

    [Column("scenario_key"), StringLength(64)]
    public string ScenarioKey { get; set; }

    [Column("channel"), StringLength(32)]
    public string Channel { get; set; } = "sms";

    [Column("sender_number"), StringLength(32)]
    public string SenderNumber { get; set; }

    [Column("customer_number"), StringLength(32)]
    public string CustomerNumber { get; set; }

    [Column("content"), StringLength(1600)]
    public string Content { get; set; }

    [Column("status")]
    public CallNotificationStatus Status { get; set; }

    [Column("failure_reason"), StringLength(2048)]
    public string FailureReason { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Column("twilio_message_sid"), StringLength(64)]
    public string TwilioMessageSid { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [Column("sent_date")]
    public DateTimeOffset? SentDate { get; set; }

    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}
