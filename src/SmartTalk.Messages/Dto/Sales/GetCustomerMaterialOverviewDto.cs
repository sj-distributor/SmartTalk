using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Sales;

public class GetCustomerMaterialOverviewRequestDto
{
    [JsonProperty("customerNumbers")]
    public List<string> CustomerNumbers { get; set; }
}

public class GetCustomerMaterialOverviewResponseDto
{
    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("data")]
    public List<CustomerMaterialOverviewDto> Data { get; set; }
}

public class CustomerMaterialOverviewDto
{
    [JsonProperty("customerNumber")]
    public string CustomerNumber { get; set; }

    [JsonProperty("items")]
    public List<CustomerMaterialItemDto> Items { get; set; } = [];

    [JsonProperty("level5Habits")]
    public List<CustomerMaterialLevel5HabitDto> Level5Habits { get; set; } = [];
}

public class CustomerMaterialItemDto
{
    [JsonProperty("sourceType")]
    public string SourceType { get; set; }

    [JsonProperty("materialNumber")]
    public string MaterialNumber { get; set; }

    [JsonProperty("materialDescription")]
    public string MaterialDescription { get; set; }

    [JsonProperty("plant")]
    public string Plant { get; set; }

    [JsonProperty("materialType")]
    public string MaterialType { get; set; }

    [JsonProperty("levelCode5")]
    public string LevelCode5 { get; set; }

    [JsonProperty("baseUnit")]
    public string BaseUnit { get; set; }

    [JsonProperty("salesUnit")]
    public string SalesUnit { get; set; }

    [JsonProperty("weight")]
    public decimal Weight { get; set; }

    [JsonProperty("placeOfOrigin")]
    public string PlaceOfOrigin { get; set; }

    [JsonProperty("packing")]
    public string Packing { get; set; }

    [JsonProperty("specifications")]
    public string Specifications { get; set; }

    [JsonProperty("rank")]
    public string Rank { get; set; }

    [JsonProperty("atr")]
    public decimal Atr { get; set; }

    [JsonProperty("goodsStatus")]
    public string GoodsStatus { get; set; }

    [JsonProperty("goodsComments")]
    public string GoodsComments { get; set; }

    [JsonProperty("price")]
    public decimal? Price { get; set; }

    [JsonProperty("lastInvoiceDate")]
    public DateTime? LastInvoiceDate { get; set; }
}

public class CustomerMaterialLevel5HabitDto
{
    [JsonProperty("levelCode5")]
    public string LevelCode5 { get; set; }

    [JsonProperty("customerLikeNames")]
    public List<CustomerLikeNameDto> CustomerLikeNames { get; set; } = [];
}
