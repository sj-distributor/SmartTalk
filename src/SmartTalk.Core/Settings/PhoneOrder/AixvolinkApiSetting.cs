using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.PhoneOrder;

public class AixvolinkApiSetting : IConfigurationSetting
{
    public AixvolinkApiSetting(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("Aixvolink:Api:BaseUrl");
        ApiKey = configuration.GetValue<string>("Aixvolink:Api:ApiKey");
    }

    public string BaseUrl { get; set; }

    public string ApiKey { get; set; }
}
