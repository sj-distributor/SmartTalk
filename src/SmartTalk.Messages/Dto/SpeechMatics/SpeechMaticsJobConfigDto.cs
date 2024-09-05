using Newtonsoft.Json;
using SmartTalk.Messages.Converters;
using SmartTalk.Messages.Enums.SpeechMatics;

namespace SmartTalk.Messages.Dto.SpeechMatics;

public class SpeechMaticsJobConfigDto
{
    [JsonProperty("type")]
    [JsonConverter(typeof(LowerFirstLetterEnumConverter), typeof(SpeechMaticsJobType))]
    public SpeechMaticsJobType Type { get; set; } = SpeechMaticsJobType.Transcription;
    
    [JsonProperty("transcription_config", NullValueHandling = NullValueHandling.Ignore)]
    public SpeechMaticsTranscriptionConfigDto? TranscriptionConfig { get; set; }
    
    [JsonProperty("notification_config", NullValueHandling = NullValueHandling.Ignore)]
    public List<SpeechMaticsNotificationConfigDto>? NotificationConfig { get; set; }
}