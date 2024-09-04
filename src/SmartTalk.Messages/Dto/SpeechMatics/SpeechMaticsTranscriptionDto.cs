using Newtonsoft.Json;
using SmartTalk.Messages.Converters;
using SmartTalk.Messages.Enums.Speechmatics;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechMaticsTranscriptionDto
{
    [JsonProperty("type")]
    public string Type { get; set; }
    
    [JsonProperty("transcription_config")]
    public SpeechMaticsTranscriptionConfigDto TranscriptionConfig { get; set; }
    
    [JsonProperty("translation_config")]
    public SpeechMaticsTranslationConfigDto TranslationConfig { get; set; }
}

public class SpeechMaticsTranscriptionConfigDto
{
    [JsonProperty("operating_point")]
    [JsonConverter(typeof(LowerFirstLetterEnumConverter), typeof(SpeechMaticsOperatingPointType))]
    public SpeechMaticsOperatingPointType OperatingPoint { get; set; }
    
    [JsonProperty("language")]
    [JsonConverter(typeof(LowerFirstLetterEnumConverter), typeof(SpeechMaticsLanguageType))]
    public SpeechMaticsLanguageType Language { get; set; }
    
    [JsonProperty("diarization")]
    [JsonConverter(typeof(LowerFirstLetterEnumConverter), typeof(SpeechMaticsDiarizationType))]
    public SpeechMaticsDiarizationType Diarization { get; set; }
}

public class SpeechMaticsTranslationConfigDto
{
    [JsonProperty("target_languages")]
    public List<string> TargetLanguages { get; set; }
}