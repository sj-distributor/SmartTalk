using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.PhoneOrder;

public class SchedulingPhoneOrderDailyDataBroadcastRecurringJobExpressionSetting : IConfigurationSetting<string>
{
    public SchedulingPhoneOrderDailyDataBroadcastRecurringJobExpressionSetting(IConfiguration config)
    {
        Value = config.GetValue<string>("SchedulingPhoneOrderDailyDataBroadcastRecurringJobExpression");
    }

    public string Value { get; set; }
}