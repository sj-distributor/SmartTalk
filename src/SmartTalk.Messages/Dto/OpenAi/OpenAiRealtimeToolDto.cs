using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.OpenAi;

public class OpenAiRealtimeToolDto
{
    [JsonProperty("type")]
    public string Type { get; set; }
    
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("description")]
    public string Description { get; set; }
    
    [JsonProperty("parameters")]
    public OpenAiRealtimeToolParametersDto Parameters { get; set; }
}

public class OpenAiRealtimeToolParametersDto
{
    [JsonProperty("type")]
    public string Type { get; set; }
    
    [JsonProperty("properties")]
    public object Properties { get; set; }
}