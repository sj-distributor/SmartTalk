using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.PhoneOrder;

[Table("phone_order_reservation_information")]
public class PhoneOrderReservationInformation : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("record_id")]
    public int RecordId { get; set; }

    [Column("reservation_date")]
    public string ReservationDate { get; set; }

    [Column("reservation_time")]
    public string ReservationTime { get; set; }

    [Column("user_name")]
    public string UserName { get; set; }

    [Column("party_size")]
    public int? PartySize { get; set; }

    [Column("special_requests")]
    public string SpecialRequests { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}