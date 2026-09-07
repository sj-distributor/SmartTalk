using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Twilio;

[Table("sms_customer_opt_out")]
public class SmsCustomerOptOut : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("customer_phone_number"), StringLength(32)]
    public string CustomerPhoneNumber { get; set; }

    [Column("opted_out_at")]
    public DateTimeOffset OptedOutAt { get; set; } = DateTimeOffset.UtcNow;
}
