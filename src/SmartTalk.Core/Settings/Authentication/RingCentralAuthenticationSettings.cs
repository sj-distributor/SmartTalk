using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Authentication;

public class RingCentralAuthenticationSettings(IConfiguration configuration) : IConfigurationSetting
{
    public string BaseUrl { get; set; } = configuration.GetValue<string>("Authentication:RingCentral:BaseUrl");
    
    public string BasicAuth { get; set; } = configuration.GetValue<string>("Authentication:RingCentral:BasicAuth");
    
    public string JwtAssertion { get; set; } = configuration.GetValue<string>("Authentication:RingCentral:JwtAssertion");
}