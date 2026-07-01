using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.OpenAi;

public class OpenAiSettings : IConfigurationSetting
{
    public const string DefaultRecordAnalyzeModel = "gpt-audio-1.5";

    public OpenAiSettings(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("OpenAi:BaseUrl");
        ApiKey = configuration.GetValue<string>("OpenAi:ApiKey");
        Organization = configuration.GetValue<string>("OpenAi:Organization");
        RecordAnalyzeModel = configuration.GetValue<string>("OpenAi:RecordAnalyzeModel");

        if (string.IsNullOrWhiteSpace(RecordAnalyzeModel))
            RecordAnalyzeModel = DefaultRecordAnalyzeModel;
        
        HkBaseUrl = configuration.GetValue<string>("OpenAiForHk:BaseUrl");
        HkApiKey = configuration.GetValue<string>("OpenAiForHk:ApiKey");
        HkOrganization = configuration.GetValue<string>("OpenAiForHk:Organization");
    }
    
    // Us 
    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }
    
    public string Organization { get; set; }

    public string RecordAnalyzeModel { get; set; }
    
    // Hk 
    public string HkBaseUrl { get; set; }
    
    public string HkApiKey { get; set; }
    
    public string HkOrganization { get; set; }
}
