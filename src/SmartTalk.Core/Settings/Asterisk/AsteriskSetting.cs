using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Asterisk;

public class AsteriskSetting : IConfigurationSetting
{
    public AsteriskSetting(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("Asterisk:BaseUrl");
    }

    public string BaseUrl { get; set; }
}