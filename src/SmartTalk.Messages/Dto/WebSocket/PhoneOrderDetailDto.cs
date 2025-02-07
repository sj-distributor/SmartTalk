using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.WebSocket;

public class PhoneOrderDetailDto
{
    [JsonProperty("food_details")]
    public List<FoodDetailDto> FoodDetails { get; set; } = new();
    
    [JsonProperty("client_phone_number")]
    public string ClientPhoneNumber { get; set; }
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