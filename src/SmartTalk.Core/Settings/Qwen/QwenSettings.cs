using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Qwen;

public class QwenSettings : IConfigurationSetting
{
    public QwenSettings(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("Qwen:ApiKey");
    }

    public string ApiKey { get; set; }
}