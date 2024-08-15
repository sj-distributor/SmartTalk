using Microsoft.AspNetCore.Authentication;

namespace Smarties.Api.Authentication.OME;

public class OMEAuthenticationOptions : AuthenticationSchemeOptions
{
    public string Authority { get; set; }
    
    public string AppId { get; set; }
    
    public string AppSecret { get; set; }
}