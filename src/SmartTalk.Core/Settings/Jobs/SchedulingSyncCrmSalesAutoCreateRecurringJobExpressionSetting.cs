using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Jobs;

public class SchedulingSyncCrmSalesAutoCreateRecurringJobExpressionSetting : IConfigurationSetting<string>
{
    public SchedulingSyncCrmSalesAutoCreateRecurringJobExpressionSetting(IConfiguration configuration)
    {
        Value = configuration.GetValue<string>("SchedulingSyncCrmSalesAutoCreateRecurringJobCronExpression");
    }

    public string Value { get; set; }
}
