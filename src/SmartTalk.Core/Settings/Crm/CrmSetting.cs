using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Crm;

public class CrmSetting : IConfigurationSetting
{
    public CrmSetting(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("Crm:BaseUrl");
        
        ClientId = configuration.GetValue<string>("Crm:ClientId");
        
        ClientSecret = configuration.GetValue<string>("Crm:ClientSecret");
        
        SyncBaseUrl = configuration.GetValue<string>("Crm:SyncBaseUrl");
        
        ApiKey = configuration.GetValue<string>("Crm:ApiKey");
    }

    public string BaseUrl { get; set; }
    
    public string ClientId { get; set; }
    
    public string ClientSecret { get; set; }
    
    public string SyncBaseUrl { get; set; }  
    
    public string ApiKey { get; set; }
}