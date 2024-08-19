using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Speechmatics;

public class SpeechmaticsSettings : IConfigurationSetting
{
    public SpeechmaticsSettings(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("Speechmatics:ApiKey");
        BaseUrl = configuration.GetValue<string>("Speechmatics:BaseUrl");
    }
    
    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }
}