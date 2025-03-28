using SmartTalk.Messages.Enums.SipServer;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Asterisk;

[Table("restaurant_asterisk")]
public class RestaurantAsterisk : IEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("host_id")]
    public int HostId { get; set; }

    [Column("endpoint")]
    public string Endpoint { get; set; }

    [Column("host_records")]
    public string HostRecords { get; set; }
    
    [Column("restaurant_phone_number")]
    public string RestaurantPhoneNumber { get; set; }

    [Column("twilio_number")]
    public string TwilioNumber { get; set; }

    [Column("domain_name")]
    public string DomainName { get; set; }
    
    [Column("phone_path_status")]
    public PhonePathStatus PhonePathStatus { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [NotMapped] 
    public string CdrBaseUrl => "http://" + HostRecords + "." + DomainName + ":5000";
}