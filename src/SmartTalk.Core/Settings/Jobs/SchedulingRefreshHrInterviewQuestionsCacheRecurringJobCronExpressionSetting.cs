using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Jobs;

public class SchedulingRefreshHrInterviewQuestionsCacheRecurringJobCronExpressionSetting : IConfigurationSetting<string>
{
    public SchedulingRefreshHrInterviewQuestionsCacheRecurringJobCronExpressionSetting(IConfiguration configuration)
    {
        Value = configuration.GetValue<string>("SchedulingRefreshHrInterviewQuestionsCacheRecurringJobCronExpression");
    }
    
    public string Value { get; set; }
}