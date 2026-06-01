using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.SjFood;

public class SjFoodSetting : IConfigurationSetting
{
    public SjFoodSetting(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("SjFood:ApiKey");
        BaseUrl = configuration.GetValue<string>("SjFood:BaseUrl");
    }

    public string ApiKey { get; set; }

    public string BaseUrl { get; set; }
}
