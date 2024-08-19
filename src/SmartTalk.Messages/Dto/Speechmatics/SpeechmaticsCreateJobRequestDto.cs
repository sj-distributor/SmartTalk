using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechmaticsCreateJobRequestDto
{
    [JsonProperty("config")] 
    public SpeechmaticsJobConfigDto SpeechmaticsJobConfigDto { get; set; }
    
    // [JsonProperty("data_file", NullValueHandling = NullValueHandling.Ignore)]
    // public string DataFile { get; set; }
    //
    // [JsonProperty("text_file", NullValueHandling = NullValueHandling.Ignore)]
    // public string TextFile { get; set; }
}

public class SpeechmaticsGetAllJobsRequestDto
{
    [JsonProperty("created_before", NullValueHandling = NullValueHandling.Ignore)]
    public string CreatedBefore { get; set; }

    [JsonProperty("limit", NullValueHandling = NullValueHandling.Ignore)]
    public int? Limit { get; set; }

    [JsonProperty("include_deleted", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IncludeDeleted { get; set; }
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