using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.PhoneOrder;

[Table("phone_order_order_item")]
public class PhoneOrderOrderItem : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("conversation_id")]
    public int ConversationId { get; set; }
    
    [Column("food_name")]
    public string FoodName { get; set; }
    
    [Column("quantity")]
    public int Quantity { get; set; }
    
    [Column("price")]
    public double Price { get; set; }
    
    [Column("note")]
    public string Note { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}