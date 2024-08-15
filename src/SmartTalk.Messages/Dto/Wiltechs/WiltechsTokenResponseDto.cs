using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Wiltechs;

public class WiltechsTokenResponseDto
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; }
    
    [JsonProperty("token_type")]
    public string TokenType { get; set; }
    
    [JsonProperty("expires_in")]
    public long ExpiresIn { get; set; }
    
    [JsonProperty("userName")]
    public string Username { get; set; }
    
    [JsonProperty("error")]
    public string Error { get; set; }
    
    [JsonProperty("error_description")]
    public string ErrorDescription { get; set; }
}