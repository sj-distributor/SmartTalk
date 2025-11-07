using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.SapGateway;

public class SapGatewaySetting : IConfigurationSetting
{
    public SapGatewaySetting(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("SapGateway:ApiKey");
        BaseUrl = configuration.GetValue<string>("SapGateway:BaseUrl");
    }
    
    public string ApiKey { get; set; }
    
    public string BaseUrl { get; set; }
}