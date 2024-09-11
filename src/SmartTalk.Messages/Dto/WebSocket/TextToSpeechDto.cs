using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.WebSocket;

public class TextToSpeechDto
{
    [JsonProperty("text")]
    public string Text { get; set; }

    [JsonProperty("speed")] 
    public int Speed { get; set; } = 1;

    [JsonProperty("voice_id")]
    public int VoiceId
    {
        get;
        set;
    }

    [JsonProperty("sample_rate")] 
    public int SampleRate { get; set; } = 8000;
        
    [JsonProperty("file_format")] 
    public string FileFormat { get; set; } = "wav";

    [JsonProperty("response_format")] 
    public string ResponseFormat { get; set; } = "url";
}