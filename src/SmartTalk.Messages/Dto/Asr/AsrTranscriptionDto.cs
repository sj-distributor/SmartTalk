using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Asr;

public class AsrTranscriptionDto
{
    [JsonProperty("file")]
    public byte[] File { get; set; }
    
    public string FileName { get; set; }
    
    public string Language { get; set; }
    
    public string ResponseFormat { get; set; }
}

public class AsrTranscriptionResponseDto
{
    [JsonProperty("text")]
    public string Text { get; set; }
}