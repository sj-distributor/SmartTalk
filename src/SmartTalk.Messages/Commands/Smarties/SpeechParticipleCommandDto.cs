using Newtonsoft.Json;
using Smarties.Messages.Responses;

namespace SmartTalk.Messages.Commands.Smarties;

public class SpeechParticipleCommandDto
{
    public string Url { get; set; }
}

public class SpeechParticipleResponse : SmartiesResponse<SpeechParticipleResponseDto>
{
}

public class SpeechParticipleResponseDto
{
    [JsonProperty("status")]
    public int Status { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("result")]
    public Dictionary<string, SpeechParticipleResultDto> Result { get; set; }
}

public class SpeechParticipleResultDto
{
    [JsonProperty("start_time")]
    public string StartTime { get; set; }
    
    [JsonProperty("end_time")]
    public string EndTime { get; set; }
    
    [JsonProperty("text")]
    public string Text { get; set; }
}