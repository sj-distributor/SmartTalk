using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Qwen;

public class QwenSettings : IConfigurationSetting
{
    public QwenSettings(IConfiguration configuration)
    {
        CrmApiKey = configuration.GetValue<string>("Qwen:Crm:ApiKey");
    }

    public string CrmApiKey { get; set; }
}