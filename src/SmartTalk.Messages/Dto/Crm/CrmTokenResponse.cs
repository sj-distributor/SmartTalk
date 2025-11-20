using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Crm;

public class CrmTokenResponse
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; }
}