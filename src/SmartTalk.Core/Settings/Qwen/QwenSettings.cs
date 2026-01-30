using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Qwen;

public class QwenSettings : IConfigurationSetting
{
    public QwenSettings(IConfiguration configuration)
    {
        CrmBaseUrl = configuration.GetValue<string>("Qwen:Crm:BaseUrl");
        CrmApiKey = configuration.GetValue<string>("Qwen:Crm:ApiKey");
    }

    public string CrmBaseUrl { get; }
    public string CrmApiKey { get; set; }
}