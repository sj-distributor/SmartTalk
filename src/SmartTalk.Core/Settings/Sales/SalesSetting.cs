using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Sales;

public class SalesSetting : IConfigurationSetting
{
    public SalesSetting(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("Sales:ApiKey");
        BaseUrl = configuration.GetValue<string>("Sales:BaseUrl");
        CompanyName = configuration.GetValue<string>("Sales:CompanyName");
    }
    
    public string ApiKey { get; set; }
    
    public string BaseUrl { get; set; }

    public string CompanyName { get; set; }
}
