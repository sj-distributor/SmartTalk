using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.OpenAi;

public class OpenAiRealtimeSessionDto
{
    [JsonProperty("model")]
    public string Model { get; set; }
    
    [JsonProperty("turn_detection")]
    public object TurnDetection { get; set; }

    [JsonProperty("voice")]
    public string Voice { get; set; }

    [JsonProperty("instructions")]
    public string Instructions { get; set; }

    [JsonProperty("modalities")]
    public List<string> Modalities { get; set; }

    [JsonProperty("input_audio_transcription")]
    public object InputAudioTranscription { get; set; }

    [JsonProperty("tools")]
    public List<object> Tools { get; set; }
}