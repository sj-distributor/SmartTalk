using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.SpeechMatics;

public class SpeechMaticsSettings : IConfigurationSetting
{
    public SpeechMaticsSettings(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("Speechmatics:ApiKey");
        BaseUrl = configuration.GetValue<string>("Speechmatics:BaseUrl");
    }
    
    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }
}