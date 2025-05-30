using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.OpenAi;

public class OpenAiTrainingSettings : IConfigurationSetting
{
    public OpenAiTrainingSettings(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("OpenAiTraining:BaseUrl");
        ApiKey = configuration.GetValue<string>("OpenAiTraining:ApiKey");
        Organization = configuration.GetValue<string>("OpenAiTraining:Organization");
        OpenAiTrainingCronExpression = configuration.GetValue<string>("OpenAiTraining:OpenAiTrainingCronExpression");
    }
    
    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }
    
    public string Organization { get; set; }
    
    public string OpenAiTrainingCronExpression { get; set; }
}