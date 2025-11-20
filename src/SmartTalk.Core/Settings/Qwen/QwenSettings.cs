using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Qwen;

public class QwenSettings : IConfigurationSetting
{
    public QwenSettings(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("Qwen:BaseUrl");
        ApiKey = configuration.GetValue<string>("Qwen:ApiKey");
    }
    
    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }
}