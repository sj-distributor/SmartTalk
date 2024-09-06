using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.SpeechMatics;

public class SpeechMaticsCreateJobRequestDto
{
    [JsonProperty("config")]
    public SpeechMaticsJobConfigDto JobConfig { get; set; }
}

public class SpeechMaticsCreateTranscriptionDto
{
    public byte[] Data { get; set; }
    
    public string FileName { get; set; }
}