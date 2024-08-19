using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechmaticsJobDetailDto
{
    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("data_name")]
    public string DataName { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }
    
    [JsonProperty("duration")]
    public int? Duration { get; set; }
    
    [JsonProperty("config")]
    public SpeechmaticsJobConfigDto? Config { get; set; }
}