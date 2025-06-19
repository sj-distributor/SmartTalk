using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Google;

public class GoogleSettings : IConfigurationSetting
{
    public GoogleSettings(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("Google:ApiKey");
    }
    
    public string ApiKey { get; set; }
}