using Newtonsoft.Json;
using SmartTalk.Messages.Converters;
using SmartTalk.Messages.Enums.Speechmatics;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechmaticsJobConfigDto
{
    [JsonProperty("type")]
    [JsonConverter(typeof(LowerFirstLetterEnumConverter), typeof(JobType))]
    public JobType Type { get; set; } = JobType.Transcription;
    
    [JsonProperty("transcription_config", NullValueHandling = NullValueHandling.Ignore)]
    public SpeechmaticsTranscriptionConfigDto? TranscriptionConfig { get; set; }
    
    [JsonProperty("notification_config", NullValueHandling = NullValueHandling.Ignore)]
    public SpeechmaticsNotificationConfigDto? NotificationConfig { get; set; }
}