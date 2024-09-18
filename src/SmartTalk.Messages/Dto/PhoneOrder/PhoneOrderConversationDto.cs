using Newtonsoft.Json;
using SmartTalk.Messages.Enums.PhoneOrder;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Messages.Dto.PhoneOrder;

[Table("phone_order_conversation")]
public class PhoneOrderConversationDto
{
    public int Id { get; set; }

    public int RecordId { get; set; }

    public string Question { get; set; }

    public string Answer { get; set; }

    public PhoneOrderIntent Intent { get; set; } = PhoneOrderIntent.Chat;
    
    public int Order { get; set; }

    public List<ExtractFoodItemDto> ExtractFoodItemOjects { get; set; }
    
    public string ExtractFoodItem => JsonConvert.SerializeObject(ExtractFoodItemOjects);
    
    public DateTimeOffset CreatedDate { get; set; }
}

public class ExtractFoodItemDto
{
    [JsonProperty("food_name")]
    public string FoodName { get; set; }
        
    [JsonProperty("count")]
    public int Count { get; set; }
    
    [JsonProperty("price")]
    public double Price { get; set; }
    
    [JsonProperty("remark")]
    public string Remark { get; set; }
}