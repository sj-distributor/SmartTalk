using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.PhoneOrder;

[Table("phone_order_reservation_information")]
public class PhoneOrderReservationInformation : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("record_id")]
    public int RecordId { get; set; }

    [Column("notification_info")]
    public string NotificationInfo { get; set; }
    
    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }
    
    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}