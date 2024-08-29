using Newtonsoft.Json;
using SmartTalk.Messages.Converters;
using SmartTalk.Messages.Enums.Speechmatics;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechmaticsJobConfigDto
{
    [JsonProperty("type")]
    [JsonConverter(typeof(LowerFirstLetterEnumConverter), typeof(SpeechmaticsJobType))]
    public SpeechmaticsJobType Type { get; set; } = SpeechmaticsJobType.Transcription;
    
    [JsonProperty("transcription_config", NullValueHandling = NullValueHandling.Ignore)]
    public SpeechmaticsTranscriptionConfigDto? TranscriptionConfig { get; set; }
    
    [JsonProperty("notification_config", NullValueHandling = NullValueHandling.Ignore)]
    public SpeechmaticsNotificationConfigDto? NotificationConfig { get; set; }
}