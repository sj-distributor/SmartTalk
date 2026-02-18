using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.OpenAi;

public class OpenAiSettings : IConfigurationSetting
{
    public OpenAiSettings(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("OpenAi:BaseUrl");
        ApiKey = configuration.GetValue<string>("OpenAi:ApiKey");
        Organization = configuration.GetValue<string>("OpenAi:Organization");
        
        RealtimeSendBuffLength = configuration.GetValue<int>("OpenAi:Realtime:RealtimeSendBuffLength");
        RealtimeReceiveBufferLength = configuration.GetValue<int>("OpenAi:Realtime:ReceiveBufferLength");
        RealtimeTemperature = configuration.GetValue<float>("OpenAi:Realtime:Temperature");
        RealTimeApiKeys = (configuration.GetValue<string>("OpenAi:RealTimeApiKeys") ?? "").Split(',').Where(x => !string.IsNullOrEmpty(x)).ToList();

        HkBaseUrl = configuration.GetValue<string>("OpenAiForHk:BaseUrl");
        HkApiKey = configuration.GetValue<string>("OpenAiForHk:ApiKey");
        HkOrganization = configuration.GetValue<string>("OpenAiForHk:Organization");
    }
    
    // Us 
    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }
    
    public string Organization { get; set; }

    public int RealtimeSendBuffLength { get; set; }
    
    public int RealtimeReceiveBufferLength { get; set; }
    
    public float RealtimeTemperature { get; set; }
    
    public List<string> RealTimeApiKeys { get; set; }
    
    // Hk 
    public string HkBaseUrl { get; set; }
    
    public string HkApiKey { get; set; }
    
    public string HkOrganization { get; set; }
}