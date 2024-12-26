using Newtonsoft.Json;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Twilio;

public class GetAsteriskCdrRequest : IRequest
{
    public string Number { get; set; }
}

public class GetAsteriskCdrResponse : SmartTalkResponse<GetAsteriskCdrResponseDto>
{
}

public class GetAsteriskCdrResponseDto
{
    [JsonProperty("data")]
    public List<GetAsteriskCdrData> Data { get; set; }
}

public class GetAsteriskCdrData
{
    [JsonProperty("src")]
    public string Src { get; set; }
    
    [JsonProperty("lastapp")]
    public string LastApp { get; set; }
    
    [JsonProperty("disposition")]
    public string Disposition { get; set; }
}