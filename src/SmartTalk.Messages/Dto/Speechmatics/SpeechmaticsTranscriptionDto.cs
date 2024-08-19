using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechmaticsTranscriptionDto
{
    [JsonProperty("type")]
    public string Type { get; set; }
    
    [JsonProperty("transcription_config")]
    public SpeechmaticsTranscriptionConfigDto TranscriptionConfig { get; set; }
    
    [JsonProperty("translation_config")]
    public SpeechmaticsTranslationConfigDto TranslationConfig { get; set; }
}
public class SpeechmaticsTranscriptionConfigDto
{
    [JsonProperty("operating_point")]
    public string OperatingPoint { get; set; }
    
    [JsonProperty("language")]
    public string Language { get; set; }
    
    [JsonProperty("diarization")]
    public string Diarization { get; set; }
}

public class SpeechmaticsTranslationConfigDto
{
    [JsonProperty("target_languages")]
    public List<string> TargetLanguages { get; set; }
}