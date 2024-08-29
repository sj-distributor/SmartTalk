using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Speechmatics;

public class SpeechmaticsCreateJobRequestDto
{
    [JsonProperty("config")]
    public SpeechmaticsJobConfigDto JobConfig { get; set; }
}

public class SpeechmaticsCreateTranscriptionDto
{
    public byte[] Data { get; set; }
    
    public string FileName { get; set; }
}