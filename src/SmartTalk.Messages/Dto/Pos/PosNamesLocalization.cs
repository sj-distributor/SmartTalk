using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Pos;

public class PosNamesLocalization
{
    [JsonProperty("en")]
    public PosNamesDetail En { get; set; }
    
    [JsonProperty("cn")]
    public PosNamesDetail Cn { get; set; }
}

public class PosNamesDetail
{
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("posName")]
    public string PosName { get; set; }
    
    [JsonProperty("sendChefName")]
    public string SendChefName { get; set; }
}