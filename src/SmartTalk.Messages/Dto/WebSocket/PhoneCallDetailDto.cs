using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.WebSocket;

public class PhoneCallDetailDto
{
    [JsonProperty("food_details")]
    public List<FoodDetailDto> FoodDetails { get; set; } = new();
}

public class FoodDetailDto
{
    [JsonProperty("food_name")]
    public string FoodName { get; set; }
    
    [JsonProperty("count")]
    public string Count { get; set; }
    
    [JsonProperty("remark")]
    public string Remark { get; set; }
    
    public double Price { get; set; }
}