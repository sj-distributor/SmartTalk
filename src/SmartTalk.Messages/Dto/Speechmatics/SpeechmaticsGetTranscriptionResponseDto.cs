using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechmaticsGetTranscriptionResponseDto
{
    [JsonProperty("format")]
    public string Format { get; set; }

    [JsonProperty("job")]
    public SpeechmaticsJobInfoDto Job { get; set; }

    [JsonProperty("metadata")]
    public SpeechmaticsMetadataDto Metadata { get; set; }

    [JsonProperty("results")]
    public List<SpeechmaticsResultDto> Results { get; set; }
}

public class SpeechmaticsJobInfoDto
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

public class SpeechmaticsMetadataDto
{
    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }
}

public class SpeechmaticsResultDto
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
    public List<SpeechmaticsAlternativeDto> Alternatives { get; set; }
}

public class SpeechmaticsAlternativeDto
{
    [JsonProperty("speaker")]
    public string Speaker { get; set; }
}