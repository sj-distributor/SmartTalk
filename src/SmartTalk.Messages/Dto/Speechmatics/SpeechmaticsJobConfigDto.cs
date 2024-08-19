using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechmaticsJobConfigDto
{
    [JsonProperty("type")]
    public string Type { get; set; }
    
    [JsonProperty("transcription_config", NullValueHandling = NullValueHandling.Ignore)]
    public SpeechmaticsTranscriptionConfigDto? TranscriptionConfig { get; set; }
    
    [JsonProperty("alignment_config", NullValueHandling = NullValueHandling.Ignore)]
    public SpeechmaticsAlignmentConfigDto? AlignmentConfig { get; set; }
}