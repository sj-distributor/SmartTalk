using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneOrderOrderItemDto
{
    public int Id { get; set; }
    
    public int RecordId { get; set; }

    [JsonProperty("food_name")]
    public string FoodName { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("price")]
    public double Price { get; set; }

    public string Note { get; set; }

    public PhoneOrderOrderType OrderType { get; set; }
    
    public int MenuItemId { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
}