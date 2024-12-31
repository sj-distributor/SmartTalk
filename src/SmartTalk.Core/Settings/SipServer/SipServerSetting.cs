using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.SipServer;

public class SipServerSetting : IConfigurationSetting
{
    public SipServerSetting(IConfiguration configuration)
    {
        Source = configuration.GetValue<string>("SipServer:Source");
        Destinations = configuration.GetValue<string>("SipServer:Destinations").Split(',').ToList();
        PrivateKey = configuration.GetValue<string>("SipServer:PrivateKey");
    }
    
    public string Source { get; set; }
    
    public List<string> Destinations { get; set; }
    
    public string PrivateKey { get; set; }
}