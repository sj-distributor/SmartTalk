using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Jobs;

public class SchedulingSyncAiSpeechAssistantKnowledgePromptRecurringJobExpressionSetting : IConfigurationSetting<string>
{
    public SchedulingSyncAiSpeechAssistantKnowledgePromptRecurringJobExpressionSetting(IConfiguration configuration)
    {
        Value = configuration.GetValue<string>("SchedulingSyncAiSpeechAssistantKnowledgePromptRecurringJobCronExpression");
    }

    public string Value { get; set; }
}
