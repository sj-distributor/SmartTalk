using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Sales;

public class GetCustomerLevel5HabitResponseDto
{
    [JsonProperty("historyCustomerLevel5HabitDtos")]
    public List<HistoryCustomerLevel5HabitDto> HistoryCustomerLevel5HabitDtos { get; set; } 
}

public class HistoryCustomerLevel5HabitDto
{ 
    [JsonProperty("customerId")]
    public string CustomerId { get; set; }

    [JsonProperty("levelCode5")]
    public string LevelCode5 { get; set; }

    [JsonProperty("customerLikeNames")]
    public List<CustomerLikeNameDto> CustomerLikeNames { get; set; } = new();

    [JsonProperty("materialPartInfoDtos")]
    public List<MaterialPartInfoDto> MaterialPartInfoDtos { get; set; }
}

public class MaterialPartInfoDto
{
    [JsonProperty("materialNumber")]
    public string MaterialNumber { get; set; }

    [JsonProperty("baseUnit")]
    public string BaseUnit { get; set; }

    [JsonProperty("salesUnit")]
    public string SalesUnit { get; set; }

    [JsonProperty("weights")]
    public decimal Weights { get; set; }

    [JsonProperty("placeOfOrigin")]
    public string PlaceOfOrigin { get; set; }

    [JsonProperty("packing")]
    public string Packing { get; set; }

    [JsonProperty("specifications")]
    public string Specifications { get; set; }

    [JsonProperty("ranks")]
    public string Ranks { get; set; }

    [JsonProperty("atr")]
    public string Atr { get; set; }
}

public class CustomerLikeNameDto
{
    [JsonProperty("createDate")]
    public DateTime CreateDate { get; set; }

    [JsonProperty("customerLikeName")]
    public string CustomerLikeName { get; set; }
}