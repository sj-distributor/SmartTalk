using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Azure;

public class AzureSetting : IConfigurationSetting
{
    public AzureSetting(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("Azure:ApiKey");
    }
    
    public string ApiKey { get; set; }
}