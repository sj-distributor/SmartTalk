using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechmaticsGetUsageResponseDto
{
    [JsonProperty("since")]
    public DateTime Since { get; set; }

    [JsonProperty("until")]
    public DateTime Until { get; set; }

    [JsonProperty("summary")]
    public List<SpeechmaticsUsageDetailsDto> Summary { get; set; }

    [JsonProperty("details")]
    public List<SpeechmaticsUsageDetailsDto> Details { get; set; }
}

public class SpeechmaticsUsageDetailsDto
{
    [JsonProperty("mode")]
    public string Mode { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("count")]
    public int Count { get; set; }

    [JsonProperty("duration_hrs")]
    public double DurationHrs { get; set; }
}

public class SpeechmaticsGetUsageRequestDto
{
    [JsonProperty("since")]
    public string Since { get; set; }
    
    [JsonProperty("until")]
    public string Until { get; set; }
}