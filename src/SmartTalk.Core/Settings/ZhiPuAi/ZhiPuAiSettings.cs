using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.ZhiPuAi;

public class ZhiPuAiSettings : IConfigurationSetting
{
    public ZhiPuAiSettings(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("ZhiPuAi:ApiKey");
    }

    public string ApiKey { get; set; }
}