using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.OpenAi;

public class OpenAiRealtimeSessionsResponseDto
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("object")]
    public string Object { get; set; }

    [JsonProperty("expires_at")]
    public long ExpiresAt { get; set; }

    [JsonProperty("input_audio_noise_reduction")]
    public object InputAudioNoiseReduction { get; set; }

    [JsonProperty("turn_detection")]
    public SessionTurnDetection TurnDetection { get; set; }

    [JsonProperty("input_audio_format")]
    public string InputAudioFormat { get; set; }

    [JsonProperty("input_audio_transcription")]
    public object InputAudioTranscription { get; set; }

    [JsonProperty("client_secret")]
    public SessionClientSecret ClientSecret { get; set; }

    [JsonProperty("include")]
    public object Include { get; set; }

    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("modalities")]
    public List<string> Modalities { get; set; }

    [JsonProperty("instructions")]
    public string Instructions { get; set; }

    [JsonProperty("voice")]
    public string Voice { get; set; }

    [JsonProperty("output_audio_format")]
    public string OutputAudioFormat { get; set; }

    [JsonProperty("tool_choice")]
    public string ToolChoice { get; set; }

    [JsonProperty("temperature")]
    public double Temperature { get; set; }

    [JsonProperty("max_response_output_tokens")]
    public string MaxResponseOutputTokens { get; set; }

    [JsonProperty("tools")]
    public List<object> Tools { get; set; }
}

public class SessionTurnDetection
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("threshold")]
    public double Threshold { get; set; }

    [JsonProperty("prefix_padding_ms")]
    public int PrefixPaddingMs { get; set; }

    [JsonProperty("silence_duration_ms")]
    public int SilenceDurationMs { get; set; }

    [JsonProperty("create_response")]
    public bool CreateResponse { get; set; }

    [JsonProperty("interrupt_response")]
    public bool InterruptResponse { get; set; }
}

public class SessionClientSecret
{
    [JsonProperty("value")]
    public string Value { get; set; }

    [JsonProperty("expires_at")]
    public long ExpiresAt { get; set; }
}