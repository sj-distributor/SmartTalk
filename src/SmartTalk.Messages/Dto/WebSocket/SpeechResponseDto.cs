using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.WebSocket;

public class SpeechResponseDto
{
    [JsonProperty("status")]
    public int Status { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }
    
    [JsonProperty("result")]
    public string Result { get; set; }
}