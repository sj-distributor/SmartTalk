using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.PhoneOrder;

[Table("phone_order_conversation")]
public class PhoneOrderConversation : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("session_id")]
    public string SessionId { get; set; }
    
    [Column("restaurant_id")]
    public int RestaurantId { get; set; }
    
    [Column("tips")]
    public string Tips { get; set; }
    
    [Column("url")]
    public string Url { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}