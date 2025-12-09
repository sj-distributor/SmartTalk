using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class AiDraftOrderDto
{
    [JsonProperty("type")]
    public int Type { get; set; }
    
    [JsonProperty("items")]
    public List<AiDraftItemDto> Items { get; set; }
}

public class AiDraftItemDto
{
    [JsonProperty("productId")]
    public string ProductId { get; set; }
    
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("quantity")]
    public int Quantity { get; set; }
    
    [JsonProperty("price")]
    public string Price { get; set; }
    
    [JsonProperty("specification")]
    public string Specification { get; set; }
}