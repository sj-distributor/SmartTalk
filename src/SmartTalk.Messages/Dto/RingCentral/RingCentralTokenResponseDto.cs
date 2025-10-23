namespace SmartTalk.Messages.Dto.RingCentral;

public class RingCentralTokenResponseDto
{
    public string AccessToken { get; set; }
    
    public string TokenType { get; set; }
    
    public int ExpiresIn { get; set; }
    
    public string RefreshToken { get; set; }
    
    public int RefreshTokenExpiresIn { get; set; }
    
    public string Scope { get; set; }
    
    public string OwnerId { get; set; }
    
    public string EndpointId { get; set; }
    
    public string SessionId { get; set; }
    
    public int SessionIdleTimeout { get; set; }
}