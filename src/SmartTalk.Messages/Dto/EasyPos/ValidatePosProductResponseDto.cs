using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.EasyPos;

public class ValidatePosProductRequestDto
{
    [JsonProperty("productIds")]
    public List<long> ProductIds { get; set; }
}

public class ValidatePosProductResponseDto
{
    [JsonProperty("code")]
    public string Code { get; set; }
    
    [JsonProperty("msg")]
    public string Msg { get; set; }
    
    [JsonProperty("data")]
    public List<long> Data { get; set; }
    
    [JsonProperty("success")]
    public bool Success { get; set; }
}