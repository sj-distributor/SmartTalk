using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.WebSocket;

public class RecordingFinishedEventDto
{
    public string Type { get; set; }
    
    public string timestamp { get; set; }
    
    public string dialstring { get; set; }
    
    public RecordingDto recording { get; set; }
    
    public ChannelDto channel { get; set; }
    
    [JsonProperty("asterisk_id")]
    public string AsteriskId { get; set; }
    
    public string application { get; set; }
}