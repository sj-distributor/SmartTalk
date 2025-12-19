using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Qwen;

public class QwenSettings : IConfigurationSetting
{
    public QwenSettings(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("Qwen:BaseUrl");
        ApiKey = configuration.GetValue<string>("Qwen:ApiKey");
        CrmApiKey = configuration.GetValue<string>("Qwen:Crm:ApiKey");
        CrmBaseUrl = configuration.GetValue<string>("Qwen:Crm:BaseUrl");
    }
    
    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }

    public string CrmApiKey { get; set; }
    
    public string CrmBaseUrl { get; set; }
}