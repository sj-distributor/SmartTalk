using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.SipServer;

public class SipServerSetting : IConfigurationSetting
{
    public SipServerSetting(IConfiguration configuration)
    {
        PrivateKey = configuration.GetValue<string>("SipServer:PrivateKey");
    }
    
    public string PrivateKey { get; set; }
}