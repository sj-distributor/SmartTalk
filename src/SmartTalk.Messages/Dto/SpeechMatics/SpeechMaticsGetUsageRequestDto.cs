using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.SpeechMatics;

public class SpeechMaticsGetUsageResponseDto
{
    [JsonProperty("since")]
    public DateTime Since { get; set; }

    [JsonProperty("until")]
    public DateTime Until { get; set; }

    [JsonProperty("summary")]
    public List<SpeechMaticsUsageDetailsDto> Summary { get; set; }

    [JsonProperty("details")]
    public List<SpeechMaticsUsageDetailsDto> Details { get; set; }
}

public class SpeechMaticsUsageDetailsDto
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

public class SpeechMaticsGetUsageRequestDto
{
    [JsonProperty("since")]
    public string Since { get; set; }
    
    [JsonProperty("until")]
    public string Until { get; set; }
}