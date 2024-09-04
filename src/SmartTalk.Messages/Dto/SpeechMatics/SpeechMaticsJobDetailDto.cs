using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechMaticsJobDetailDto
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
    public SpeechMaticsJobConfigDto? Config { get; set; }
}

public class SpeechMaticsGetAllJobsResponseDto
{
    [JsonProperty("jobs")]
    public List<SpeechMaticsJobDetailDto> JobDetails { get; set; }
}

public class SpeechMaticsGetJobDetailResponseDto : SpeechMaticsJobDetailResponseDto
{
}

public class SpeechMaticsDeleteJobResponseDto : SpeechMaticsJobDetailResponseDto
{
}

public class SpeechMaticsJobDetailResponseDto
{
    [JsonProperty("job")]
    public SpeechMaticsJobDetailDto JobDetail { get; set; }
}