using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechmaticsCreateJobRequestDto
{
    [JsonProperty("config")]
    public SpeechmaticsJobConfigDto SpeechmaticsJobConfigDto { get; set; }
}

public class SpeechmaticsGetAllJobsResponseDto
{
    [JsonProperty("jobs")]
    public List<SpeechmaticsJobDetailDto> JobDetails { get; set; }
}

public class SpeechmaticsGetJobDetailResponseDto
{
    [JsonProperty("job")]
    public SpeechmaticsJobDetailDto JobDetail { get; set; }
}

public class SpeechmaticsDeleteJobResponseDto
{
    [JsonProperty("job")]
    public SpeechmaticsJobDetailDto JobDetail { get; set; }
}