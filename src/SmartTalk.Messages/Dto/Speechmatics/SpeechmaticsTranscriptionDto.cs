using Newtonsoft.Json;
using SmartTalk.Messages.Converters;
using SmartTalk.Messages.Enums.Speechmatics;

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
    [JsonConverter(typeof(LowerFirstLetterEnumConverter), typeof(SpeechmaticsOperatingPointType))]
    public SpeechmaticsOperatingPointType OperatingPoint { get; set; }
    
    [JsonProperty("language")]
    [JsonConverter(typeof(LowerFirstLetterEnumConverter), typeof(SpeechmaticsLanguageType))]
    public SpeechmaticsLanguageType Language { get; set; }
    
    [JsonProperty("diarization")]
    [JsonConverter(typeof(LowerFirstLetterEnumConverter), typeof(SpeechmaticsDiarizationType))]
    public SpeechmaticsDiarizationType Diarization { get; set; }
}

public class SpeechmaticsTranslationConfigDto
{
    [JsonProperty("target_languages")]
    public List<string> TargetLanguages { get; set; }
}