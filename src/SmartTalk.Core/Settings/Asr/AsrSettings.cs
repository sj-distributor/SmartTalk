using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Asr;

public class AsrSettings(IConfiguration configuration) : IConfigurationSetting
{
    public string BaseUrl => configuration.GetValue<string>("Asr:BaseUrl");
    
    public string Authorization => configuration.GetValue<string>("Asr:Authorization");
}