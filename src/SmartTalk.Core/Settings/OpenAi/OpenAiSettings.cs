using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.OpenAi;

public class OpenAiSettings : IConfigurationSetting
{
    public OpenAiSettings(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("OpenAi:BaseUrl");
        ApiKey = configuration.GetValue<string>("OpenAi:ApiKey");
        Organization = configuration.GetValue<string>("OpenAi:Organization");
        RealTimeApiKeys = (configuration.GetValue<string>("OpenAi:RealTimeApiKeys") ?? "").Split(',').Where(x => !string.IsNullOrEmpty(x)).ToList();
    }
    
    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }
    
    public string Organization { get; set; }
    
    public List<string> RealTimeApiKeys { get; set; }
}