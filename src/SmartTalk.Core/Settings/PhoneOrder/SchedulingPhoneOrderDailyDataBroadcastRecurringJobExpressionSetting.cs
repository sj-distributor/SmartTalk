using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.PhoneOrder;

public class SchedulingPhoneOrderDailyDataBroadcastRecurringJobExpressionSetting : IConfigurationSetting<string>
{
    public SchedulingPhoneOrderDailyDataBroadcastRecurringJobExpressionSetting(IConfiguration config)
    {
        RobotUrl = JsonSerializer.Deserialize<List<string>>(config.GetValue<string>("DataBroadcastRobot"));
        Value = config.GetValue<string>("SchedulingPhoneOrderDailyDataBroadcastRecurringJobExpression");
    }

    public string Value { get; set; }

    public List<string> RobotUrl { get; set; }
}