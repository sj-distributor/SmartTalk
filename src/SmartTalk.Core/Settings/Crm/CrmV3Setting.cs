using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Crm;

public class CrmV3Setting: IConfigurationSetting
{
    public CrmV3Setting(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("CrmV3:BaseUrl");
        
        ApiKey = configuration.GetValue<string>("CrmV3:ApiKey");
    }

    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }
}