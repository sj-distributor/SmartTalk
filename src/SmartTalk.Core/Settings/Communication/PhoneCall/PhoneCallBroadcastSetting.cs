using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Communication.PhoneCall;

public class PhoneCallBroadcastSetting : IConfigurationSetting
{
    public PhoneCallBroadcastSetting(IConfiguration configuration)
    {
        BroadcastUrl = configuration.GetValue<string>("PhoneCallBroadcast:BroadcastUrl");
        PhoneNumber = configuration.GetValue<string>("PhoneCallBroadcast:PhoneNumber");
    }
    
    public string BroadcastUrl { get; set; }

    public string PhoneNumber { get; set; }
}