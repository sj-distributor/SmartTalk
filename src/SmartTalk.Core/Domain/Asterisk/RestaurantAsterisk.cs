using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Asterisk;

[Table("restaurant_asterisk")]
public class RestaurantAsterisk : IEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("restaurant_phone_number")]
    public string RestaurantPhoneNumber { get; set; }

    [Column("twilio_number")]
    public string TwilioNumber { get; set; }

    [Column("cdr_domain_name")]
    public string CdrDomainName { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}