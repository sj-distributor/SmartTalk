using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechmaticsAlignmentDto
{
    [JsonProperty("type")]
    public string Type { get; set; }
    
    [JsonProperty("alignment_config")]
    public SpeechmaticsAlignmentConfigDto Alignment { get; set; }
}

public class SpeechmaticsAlignmentConfigDto
{
    [JsonProperty("language")] 
    public string Language { get; set; }
}
