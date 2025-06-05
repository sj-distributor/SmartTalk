using Newtonsoft.Json;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Dto.EasyPos;

public class EasyPosTokenRequestDto
{
    [JsonProperty("appId")]
    public string AppId { get; set; }
    
    [JsonProperty("appSecret")]
    public string AppSecret { get; set; }
}

public class EasyPosTokenResponseDto
{
    [JsonProperty("code")]
    public string Code { get; set; }
    
    [JsonProperty("msg")]
    public string Msg { get; set; }
    
    [JsonProperty("data")]
    public string Data { get; set; }
    
    [JsonProperty("success")]
    public bool Success { get; set; }
}