using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Domain.PhoneOrder;

[Table("phone_order_record")]
public class PhoneOrderRecord : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("session_id")]
    public string SessionId { get; set; }
    
    [Column("restaurant")]
    public PhoneOrderRestaurant Restaurant { get; set; }
    
    [Column("tips")]
    public string Tips { get; set; }
    
    [Column("transcription_text")]
    public string TranscriptionText { get; set; }
    
    [Column("url")]
    public string Url { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}