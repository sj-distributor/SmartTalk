using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.RingCentral;

public class RingCentralTokenResponseDto
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; }
    
    [JsonProperty("token_type")]
    public string TokenType { get; set; }
    
    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }
    
    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; }
    
    [JsonProperty("refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; set; }
    
    [JsonProperty("scope")]
    public string Scope { get; set; }
    
    [JsonProperty("owner_id")]
    public string OwnerId { get; set; }
    
    [JsonProperty("endpoint_id")]
    public string EndpointId { get; set; }
    
    [JsonProperty("session_id")]
    public string SessionId { get; set; }
    
    [JsonProperty("session_idle_timeout")]
    public int SessionIdleTimeout { get; set; }
}