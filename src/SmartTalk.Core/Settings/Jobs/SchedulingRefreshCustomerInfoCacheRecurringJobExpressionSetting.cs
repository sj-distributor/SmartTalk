using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Jobs;

public class SchedulingRefreshCustomerInfoCacheRecurringJobExpressionSetting : IConfigurationSetting<string>
{
    public SchedulingRefreshCustomerInfoCacheRecurringJobExpressionSetting(IConfiguration configuration)
    {
        Value = configuration.GetValue<string>("SchedulingRefreshCustomerInfoCacheRecurringJobCronExpression");
    }

    public string Value { get; set; }
}