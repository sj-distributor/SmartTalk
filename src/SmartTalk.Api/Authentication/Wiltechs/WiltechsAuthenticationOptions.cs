using Microsoft.AspNetCore.Authentication;

namespace SmartTalk.Api.Authentication.Wiltechs;

public class WiltechsAuthenticationOptions : AuthenticationSchemeOptions
{
    public string Authority { get; set; }
    
    public List<string> Issuers { get; set; }
    
    public string SymmetricKey { get; set; }
}