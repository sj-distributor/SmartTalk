using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SmartTalk.Messages.Enums.Speechmatics;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechmaticsJobConfigDto
{
    [JsonProperty("type")]
    [JsonConverter(typeof(StringEnumConverter))]
    public JobType Type { get; set; } = JobType.Transcription;
    
    [JsonProperty("transcription_config", NullValueHandling = NullValueHandling.Ignore)]
    public SpeechmaticsTranscriptionConfigDto? TranscriptionConfig { get; set; }
}