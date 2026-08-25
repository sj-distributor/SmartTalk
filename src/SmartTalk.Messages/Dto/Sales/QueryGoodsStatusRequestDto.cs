using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Sales;

public class QueryGoodsStatusRequestDto
{
    [JsonProperty("List")]
    public List<QueryGoodsStatusItemDto> List { get; set; }
}

public class QueryGoodsStatusItemDto
{
    [JsonProperty("Material")]
    public string Material { get; set; }
    
    [JsonProperty("Plant")]
    public string Plant { get; set; }
    
    [JsonProperty("Rtype")]
    public string Rtype { get; set; }
}

public class QueryGoodsStatusResponseDto
{
    [JsonProperty("resultCode")]
    public int ResultCode { get; set; }
    
    [JsonProperty("resultMsg")]
    public string ResultMsg { get; set; }
    
    [JsonProperty("resultData")]
    public List<QueryGoodsStatusResultDto> ResultData { get; set; }
}

public class QueryGoodsStatusResultDto
{
    [JsonProperty("material")]
    public string Material { get; set; }
    
    [JsonProperty("plant")]
    public string Plant { get; set; }
    
    [JsonProperty("rtype")]
    public string Rtype { get; set; }
    
    [JsonProperty("status")]
    public string Status { get; set; }
    
    [JsonProperty("comments")]
    public string Comments { get; set; }
}
