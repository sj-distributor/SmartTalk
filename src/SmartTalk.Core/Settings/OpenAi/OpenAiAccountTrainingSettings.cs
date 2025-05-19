using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.OpenAi;

public class OpenAiAccountTrainingSettings : IConfigurationSetting
{
    public OpenAiAccountTrainingSettings(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("OpenAiAccountTraining:BaseUrl");
        ApiKey = configuration.GetValue<string>("OpenAiAccountTraining:ApiKey");
        Organization = configuration.GetValue<string>("OpenAiAccountTraining:Organization");
        OpenAiTrainingCronExpression = configuration.GetValue<string>("OpenAiAccountTraining:OpenAiTrainingCronExpression");
    }
    
    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }
    
    public string Organization { get; set; }
    
    public string OpenAiTrainingCronExpression { get; set; }
}