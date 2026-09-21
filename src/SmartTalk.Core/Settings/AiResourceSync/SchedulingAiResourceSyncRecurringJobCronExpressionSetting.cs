using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.AiResourceSync;

public class SchedulingAiResourceSyncRecurringJobCronExpression : IConfigurationSetting<string>
{
    public SchedulingAiResourceSyncRecurringJobCronExpression(IConfiguration configuration)
    {
        Value = configuration.GetValue<string>("SchedulingAiResourceSyncRecurringJobCronExpression");
    }

    public string Value { get; set; }
}