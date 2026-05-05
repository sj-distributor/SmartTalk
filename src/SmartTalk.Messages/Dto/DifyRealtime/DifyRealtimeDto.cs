using System.Text.Json.Serialization;
using Newtonsoft.Json;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Dto.DifyRealtime;

public class DifyRealtimeMessageRequest
{
    public int AssistantId { get; set; }

    public string Query { get; set; }

    public string Text { get; set; }

    public string User { get; set; }

    [JsonProperty("conversation_id")]
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }

    public RealtimeAiServerRegion Region { get; set; } = RealtimeAiServerRegion.US;

    public PhoneOrderRecordType OrderRecordType { get; set; } = PhoneOrderRecordType.TestLink;

    public int? VoiceId { get; set; }

    public string Voice { get; set; }

    public int? TimeoutSeconds { get; set; }

    public bool EndSession { get; set; }
}

public class DifyRealtimeEndSessionRequest
{
    public int AssistantId { get; set; }

    public string User { get; set; }

    [JsonProperty("conversation_id")]
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }
}

public class DifyRealtimeMessageResponse : SmartTalkResponse<DifyRealtimeMessageResponseData>;

public class DifyRealtimeEndSessionResponse : SmartTalkResponse<DifyRealtimeEndSessionResponseData>;

public class DifyRealtimeMessageResponseData
{
    public string SessionId { get; set; }

    [JsonProperty("conversation_id")]
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }

    public string Answer { get; set; }

    public bool Ended { get; set; }

    public string RecordingUrl { get; set; }
}

public class DifyRealtimeEndSessionResponseData
{
    public string SessionId { get; set; }

    [JsonProperty("conversation_id")]
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }

    public bool Ended { get; set; }

    public string RecordingUrl { get; set; }
}
