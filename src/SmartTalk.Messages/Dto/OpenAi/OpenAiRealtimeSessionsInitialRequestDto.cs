using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.OpenAi;

public class OpenAiRealtimeSessionsInitialRequestDto
{
    [JsonProperty("model")]
    public string Model { get; set; }
    
    [JsonProperty("turn_detection", NullValueHandling = NullValueHandling.Ignore)]
    public object TurnDetection { get; set; }

    [JsonProperty("input_audio_format", NullValueHandling = NullValueHandling.Ignore)]
    public string InputAudioFormat { get; set; }
    
    [JsonProperty("output_audio_format", NullValueHandling = NullValueHandling.Ignore)]
    public string OutputAudioFormat { get; set; }

    [JsonProperty("voice")]
    public string Voice { get; set; }

    [JsonProperty("instructions")]
    public string Instructions { get; set; }

    [JsonProperty("modalities", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> Modalities { get; set; }

    [JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)]
    public double Temperature { get; set; }

    [JsonProperty("input_audio_transcription")]
    public object InputAudioTranscription { get; set; }

    [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
    public List<object> Tools { get; set; }
}