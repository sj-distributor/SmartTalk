using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.EasyPos;

public class GetEasyPosTokenRequestDto
{
    [JsonProperty("appId")]
    public string AppId { get; set; }
    
    [JsonProperty("appSecret")]
    public string AppSecret { get; set; }
}