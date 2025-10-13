using System.Text.Json.Serialization;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class OpenAiAudioCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "gpt-4o-audio-preview";
    
    [JsonPropertyName("messages")]
    public object Messages { get; set; } = new();
    
    [JsonPropertyName("response_format")]
    public object ResponseFormat { get; set; } = new();
    
    [JsonPropertyName("modalities")]
    public string[] Modalities { get; set; } = Array.Empty<string>();
    
    [JsonPropertyName("audio")]
    public object Audio { get; set; }
}