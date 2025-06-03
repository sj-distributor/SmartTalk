using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Linphone;

public class LinphoneSetting : IConfigurationSetting
{
    public LinphoneSetting(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("Linphone:BaseUrl");
    }
    
    public string BaseUrl { get; set; }
}