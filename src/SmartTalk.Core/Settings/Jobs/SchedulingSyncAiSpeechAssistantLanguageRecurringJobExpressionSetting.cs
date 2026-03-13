using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Jobs;

public class SchedulingSyncAiSpeechAssistantLanguageRecurringJobExpressionSetting : IConfigurationSetting<string>
{
    public SchedulingSyncAiSpeechAssistantLanguageRecurringJobExpressionSetting(IConfiguration configuration)
    {
        Value = configuration.GetValue<string>("SchedulingSyncAiSpeechAssistantLanguageRecurringJobCronExpression");
    }

    public string Value { get; set; }
}
