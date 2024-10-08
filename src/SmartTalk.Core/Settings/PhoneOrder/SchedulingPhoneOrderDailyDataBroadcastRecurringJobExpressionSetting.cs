using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.PhoneOrder;

public class SchedulingPhoneOrderDailyDataBroadcastRecurringJobExpressionSetting : IConfigurationSetting<string>
{
    public SchedulingPhoneOrderDailyDataBroadcastRecurringJobExpressionSetting(IConfiguration config)
    {
        RobotUrl = config.GetValue<string>("DataBroadcastRobot");
        Value = config.GetValue<string>("SchedulingPhoneOrderDailyDataBroadcastRecurringJobExpression");
    }

    public string Value { get; set; }

    public string RobotUrl { get; set; }
}