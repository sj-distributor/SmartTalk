using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Linphone;

public class GetLinphoneCdrResponseDto
{
    [JsonProperty("data")]
    public List<LinphoneCdrDto> Cdrs { get; set; }
}

public class LinphoneCdrDto
{
    [JsonProperty("calldate")]
    public string CallDate { get; set; }
    
    [JsonProperty("recordingfile")] 
    public string RecordingFile { get; set; }

    [JsonProperty("disposition")]
    public string Disposition { get; set; }
}