using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Smarties;

public class SmartiesSetting : IConfigurationSetting
{
    public SmartiesSetting(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("Smarties:ApiKey");
        BaseUrl = configuration.GetValue<string>("Smarties:BaseUrl");
    }

    public string ApiKey { get; }

    public string BaseUrl { get; }
}