using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Crm;

public class CrmSetting : IConfigurationSetting
{
    public string BaseUrl { get; set; }
    
    public string ClientId { get; set; }
    
    public string ClientSecret { get; set; }
}
