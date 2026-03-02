using Newtonsoft.Json;
using SmartTalk.Messages.Dto.EasyPos;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class AiDraftOrderDto
{
    [JsonProperty("type")]
    public int Type { get; set; }
    
    [JsonProperty("phoneNumber")]
    public string PhoneNumber { get; set; }
    
    [JsonProperty("customerName")]
    public string CustomerName { get; set; }
    
    [JsonProperty("customerAddress")]
    public string CustomerAddress { get; set; }
    
    [JsonProperty("items")]
    public List<AiDraftItemDto> Items { get; set; }
    
    [JsonProperty("notes")]
    public string Notes { get; set; }
}

public class AiDraftItemDto
{
    [JsonProperty("productId")]
    public string ProductId { get; set; }
    
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("quantity")]
    public int Quantity { get; set; }
    
    [JsonProperty("specification")]
    public string Specification { get; set; }
    
    public List<AiDraftItemModifiersDto> Modifiers { get; set; }
}

public class AiDraftItemModifiersDto
{
    [JsonProperty("id")]
    public string Id { get; set; }
    
    [JsonProperty("quantity")]
    public int Quantity { get; set; }
}

public class AiDraftItemSpecificationDto
{
    [JsonProperty("modifiers")]
    public List<AiDraftItemModifiersDto> Modifiers { get; set; }
}