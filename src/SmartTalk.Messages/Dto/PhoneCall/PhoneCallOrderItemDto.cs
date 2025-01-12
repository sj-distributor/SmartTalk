using Newtonsoft.Json;
using SmartTalk.Messages.Enums.PhoneCall;

namespace SmartTalk.Messages.Dto.PhoneCall;

public class PhoneCallOrderItemDto
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

    public PhoneCallOrderType CallOrderType { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
}