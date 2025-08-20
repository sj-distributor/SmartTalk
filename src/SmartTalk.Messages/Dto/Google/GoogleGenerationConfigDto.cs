using SmartTalk.Messages.Enums.Google;

namespace SmartTalk.Messages.Dto.Google;

public class GoogleGenerationConfigDto
{
    public List<string> StopSequences { get; set; }

    public string ResponseMimeType { get; set; }

    public GoogleSchemaDto ResponseSchema { get; set; }

    public object ResponseJsonSchema { get; set; }

    public List<GoogleModalityType> ResponseModalities { get; set; }

    public int? CandidateCount { get; set; }

    public int? MaxOutputTokens { get; set; }

    public double? Temperature { get; set; }

    public double? TopP { get; set; }

    public int? TopK { get; set; }

    public int? Seed { get; set; }

    public double? PresencePenalty { get; set; }

    public double? FrequencyPenalty { get; set; }

    public bool? ResponseLogprobs { get; set; }

    public int? Logprobs { get; set; }

    public bool? EnableEnhancedCivicAnswers { get; set; }

    public GoogleSpeechConfigDto SpeechConfig { get; set; }

    public GoogleThinkingConfigDto ThinkingConfig { get; set; }

    public GoogleMediaResolutionType? MediaResolution { get; set; }
}

public class GoogleSpeechConfigDto
{
    public string LanguageCode { get; set; }
    
    public GoogleVoiceConfigDto VoiceConfig { get; set; }
    
    public GoogleMultiSpeakerConfigDto MultiSpeakerConfig { get; set; }
}

public class GoogleMultiSpeakerConfigDto
{
    public List<GoogleSpeakerConfigDto> SpeakerConfigs { get; set; }
}

public class GoogleSpeakerConfigDto
{
    public string Speaker { get; set; }
    
    public GoogleVoiceConfigDto VoiceConfig { get; set; }
}

public class GoogleVoiceConfigDto
{
    public GooglePreBuildVoiceConfigDto PreBuildVoiceConfig { get; set; }
}

public class GooglePreBuildVoiceConfigDto
{
    public string VoiceName { get; set; }
}

public class GoogleThinkingConfigDto
{
    public bool IncludeThoughts { get; set; }
    
    public int ThinkingBudget { get; set; }
}
