using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.WebSocket;

public class PhoneOrderDetailDto
{
    [JsonProperty("type")]
    public int Type { get; set; }
    
    [JsonProperty("food_details")]
    public List<FoodDetailDto> FoodDetails { get; set; } = new();
}

public class FoodDetailDto
{
    [JsonProperty("id")]
    public int Id { get; set; }
    
    [JsonProperty("food_name")]
    public string FoodName { get; set; }
    
    [JsonProperty("count")]
    public string Count { get; set; }
    
    [JsonProperty("remark")]
    public string Remark { get; set; }
    
    [JsonProperty("price")]
    public double Price { get; set; }
    
    [JsonProperty("product_id")]
    public long? ProductId { get; set; }
}