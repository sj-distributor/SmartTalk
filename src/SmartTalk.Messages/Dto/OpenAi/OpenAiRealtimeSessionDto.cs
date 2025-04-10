using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.OpenAi;

public class OpenAiRealtimeSessionDto
{
    [JsonProperty("model")]
    public string Model { get; set; }
    
    [JsonProperty("turn_detection")]
    public object TurnDetection { get; set; }

    [JsonProperty("input_audio_format")]
    public object InputAudioFormat { get; set; } = "pcm16";
    
    [JsonProperty("output_audio_format")]
    public object OutputAudioFormat { get; set; } = "pcm16";

    [JsonProperty("voice")]
    public string Voice { get; set; }

    [JsonProperty("instructions")]
    public string Instructions { get; set; }

    [JsonProperty("modalities")]
    public List<string> Modalities { get; set; }

    [JsonProperty("temperature")]
    public double Temperature { get; set; } = 0.8;

    [JsonProperty("input_audio_transcription")]
    public object InputAudioTranscription { get; set; }
    
    [JsonProperty("input_audio_noise_reduction")]
    public object InputAudioNoiseReduction { get; set; } = "near_field";

    [JsonProperty("tools")]
    public List<object> Tools { get; set; }
}