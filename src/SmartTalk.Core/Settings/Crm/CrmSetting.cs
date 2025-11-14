using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Crm;

public class CrmSetting : IConfigurationSetting
{
    public CrmSetting(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("Crm:BaseUrl");

        ClientId = configuration.GetValue<string>("Crm:ClientId");

        ClientSecret = configuration.GetValue<string>("Crm:ClientSecret");
    }

    public string BaseUrl { get; set; }

    public string ClientId { get; set; }

    public string ClientSecret { get; set; }
}