using Newtonsoft.Json;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Dto.AutoTest;

public class GetCallRecordsDataDto : SmartTalkResponse
{
    [JsonProperty("data")]
    public List<AutoTestCallLogDto> Data { get; set; }
}

public class AutoTestCallLogDto
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("direction")]
    public string Direction { get; set; }

    [JsonProperty("from")]
    public string From { get; set; }

    [JsonProperty("to")]
    public string To { get; set; }

    [JsonProperty("extension_id")]
    public string ExtensionId { get; set; }

    [JsonProperty("start_time")]
    public DateTime StartTime { get; set; }

    [JsonProperty("recording_url")]
    public string RecordingUrl { get; set; }

    [JsonProperty("call_id")]
    public string CallId { get; set; }

    [JsonProperty("source")]
    public byte Source { get; set; }
}