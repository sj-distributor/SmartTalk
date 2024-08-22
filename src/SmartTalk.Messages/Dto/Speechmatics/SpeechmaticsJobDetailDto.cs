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

public class SpeechmaticsGetAllJobsResponseDto
{
    [JsonProperty("jobs")]
    public List<SpeechmaticsJobDetailDto> JobDetails { get; set; }
}

public class SpeechmaticsGetJobDetailResponseDto : SpeechmaticsJobDetailResponseDto
{
}

public class SpeechmaticsDeleteJobResponseDto : SpeechmaticsJobDetailResponseDto
{
}

public class SpeechmaticsJobDetailResponseDto
{
    [JsonProperty("job")]
    public SpeechmaticsJobDetailDto JobDetail { get; set; }
}