using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantSessionTurnDetectionDto
{
    [JsonProperty("type")]
    public string Type { get; set; }
    
    [JsonProperty("silence_duration_ms")]
    public int SilenceDuratioMs { get; set; }
    
    [JsonProperty("threshold")]
    public float Threshold { get; set; }
}