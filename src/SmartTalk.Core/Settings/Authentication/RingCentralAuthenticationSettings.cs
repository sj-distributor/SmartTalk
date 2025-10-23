using System.Text;
using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Authentication;

public class RingCentralAuthenticationSettings(IConfiguration configuration) : IConfigurationSetting
{
    public string BaseUrl { get; set; } = configuration.GetValue<string>("Authentication:RingCentral:BaseUrl");
    
    public string ClientId { get; set; } = configuration.GetValue<string>("Authentication:RingCentral:ClientId");
    
    public string ClientSecret { get; set; } = configuration.GetValue<string>("Authentication:RingCentral:ClientSecret");
    
    public string JwtAssertion { get; set; } = configuration.GetValue<string>("Authentication:RingCentral:JwtAssertion");
    
    public string BasicAuth { get; set; } = configuration.GetValue<string>("Authentication:RingCentral:BasicAuth");
}