using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Crm;

public class CrmSetting : IConfigurationSetting
{
    public CrmSetting(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("crm:BaseUrl");
        
        AccessToken = configuration.GetValue<string>("crm:AccessToken");
    }
    
    public string BaseUrl { get; set; }
    
    public string AccessToken { get; set; } = string.Empty;
}
