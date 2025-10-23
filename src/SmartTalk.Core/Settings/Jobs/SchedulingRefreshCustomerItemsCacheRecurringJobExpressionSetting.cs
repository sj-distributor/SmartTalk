using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Jobs;

public class SchedulingRefreshCustomerItemsCacheRecurringJobExpressionSetting : IConfigurationSetting<string>
{
    public SchedulingRefreshCustomerItemsCacheRecurringJobExpressionSetting(IConfiguration configuration)
    {
        Value = configuration.GetValue<string>("SchedulingRefreshCustomerItemsCacheRecurringJobExpression");
    }
    
    public string Value { get; set; }
}