using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.SpeechMatics;

public class SpeechMaticsCallBackResponseDto
{
    [JsonProperty("format")]
    public string Format { get; set; }

    [JsonProperty("job")]
    public SpeechMaticsJobInfoDto Job { get; set; }

    [JsonProperty("metadata")]
    public SpeechMaticsMetadataDto Metadata { get; set; }

    [JsonProperty("results")]
    public List<SpeechMaticsResultDto> Results { get; set; }
}

public class SpeechMaticsJobInfoDto
{
    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("data_name")]
    public string DataName { get; set; }

    [JsonProperty("duration")]
    public int Duration { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }
}

public class SpeechMaticsMetadataDto
{
    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }
}

public class SpeechMaticsResultDto
{
    [JsonProperty("channel")]
    public string Channel { get; set; }

    [JsonProperty("start_time")]
    public double StartTime { get; set; }

    [JsonProperty("end_time")]
    public double EndTime { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } 
    
    [JsonProperty("alternatives")]
    public List<SpeechMaticsAlternativeDto> Alternatives { get; set; }
}

public class SpeechMaticsAlternativeDto
{
    [JsonProperty("speaker")]
    public string Speaker { get; set; }
    
    [JsonProperty("content")]
    public string Content { get; set; }
}