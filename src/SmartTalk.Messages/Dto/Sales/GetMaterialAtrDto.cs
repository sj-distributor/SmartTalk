using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Sales;

public class GetMaterialAtrRequestDto
{
    [JsonProperty("items")]
    public List<GetMaterialAtrItemDto> Items { get; set; } = [];
}

public class GetMaterialAtrResponseDto
{
    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("data")]
    public List<GetMaterialAtrItemDto> Data { get; set; } = [];
}

public class GetMaterialAtrItemDto
{
    [JsonProperty("customerNumber")]
    public string CustomerNumber { get; set; }

    [JsonProperty("sourceType")]
    public string SourceType { get; set; }

    [JsonProperty("plant")]
    public string Plant { get; set; }

    [JsonProperty("materialNumber")]
    public string MaterialNumber { get; set; }

    [JsonProperty("materialType")]
    public string MaterialType { get; set; }

    [JsonProperty("atr")]
    public double Atr { get; set; }
}
