using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Smarties;

public class SmartiesSetting : IConfigurationSetting
{
    public SmartiesSetting(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("Smarties:BaseUrl");
        
        ApiKey = configuration.GetValue<string>("Smarties:ApiKey");
    }
    
    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }
}