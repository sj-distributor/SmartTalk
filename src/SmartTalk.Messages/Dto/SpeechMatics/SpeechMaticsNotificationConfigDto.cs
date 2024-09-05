using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.SpeechMatics;

public class SpeechMaticsNotificationConfigDto
{
    [JsonProperty("url")]
    public string Url { get; set; }
    
    [JsonProperty("contents")]
    public List<string> Contents { get; set; }
    
    [JsonProperty("auth_headers")]
    public List<string> AuthHeaders { get; set; }
}