using System.Text.Json.Serialization;

namespace SmartTalk.Messages.Dto.RingCentral;

public class RingCentralTokenResponseDto
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }
    
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }
    
    [JsonPropertyName("refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; set; }
    
    [JsonPropertyName("scope")]
    public string Scope { get; set; }
    
    [JsonPropertyName("owner_id")]
    public string OwnerId { get; set; }
    
    [JsonPropertyName("endpoint_id")]
    public string EndpointId { get; set; }
    
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; }
    
    [JsonPropertyName("session_idle_timeout")]
    public int SessionIdleTimeout { get; set; }
}