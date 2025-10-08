using System.Text.Json.Serialization;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class OpenAiAudioCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "gpt-4o-audio-preview";
    
    [JsonPropertyName("messages")]
    public object Messages { get; set; } = new();
    
    [JsonPropertyName("response_format")]
    public ResponseFormat ResponseFormat { get; set; } = new();
    
    [JsonPropertyName("modalities")]
    public string[] Modalities { get; set; } = Array.Empty<string>();
    
    [JsonPropertyName("audio")]
    public AudioSettings? Audio { get; set; }
}

public class ResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";
}

public class AudioSettings
{
    [JsonPropertyName("voice")]
    public string Voice { get; set; } = string.Empty;
    
    [JsonPropertyName("format")]
    public string Format { get; set; } = "wav";
}