using Newtonsoft.Json;
using SmartTalk.Messages.Enums.PhoneOrder;
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
    
    [Column("record_id")]
    public int RecordId { get; set; }
    
    [Column("food_name")]
    [JsonProperty("food_name")]
    public string FoodName { get; set; }
    
    [Column("quantity")]
    [JsonProperty("quantity")]
    public int Quantity { get; set; }
    
    [Column("price")]
    [JsonProperty("price")]
    public double Price { get; set; }
    
    [Column("note")]
    public string Note { get; set; }
    
    [Column("order_type")]
    public PhoneOrderOrderType OrderType { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}