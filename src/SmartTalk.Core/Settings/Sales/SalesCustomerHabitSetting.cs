using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Sales;

public class SalesCustomerHabitSetting : IConfigurationSetting
{
    public SalesCustomerHabitSetting(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("SalesCustomerHabit:ApiKey");
        BaseUrl = configuration.GetValue<string>("SalesCustomerHabit:BaseUrl");
    }
    
    public string ApiKey { get; set; }
    
    public string BaseUrl { get; set; }
}