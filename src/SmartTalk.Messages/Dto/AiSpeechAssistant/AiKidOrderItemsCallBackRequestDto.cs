using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiKidOrderItemsCallBackRequestDto
{
    [JsonProperty("doc_date")]
    public string DocDate { get; set; }

    [JsonProperty("delivery_date")]
    public string DeliveryDate { get; set; }

    [JsonProperty("customer_id")]
    public string CustomerId { get; set; }

    [JsonProperty("items")]
    public List<AiKidOrderItemDto> Items { get; set; } = new();
}

public class AiKidOrderItemDto
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("unit")]
    public string Unit { get; set; }

    [JsonProperty("qty")]
    public int Qty { get; set; }
}
