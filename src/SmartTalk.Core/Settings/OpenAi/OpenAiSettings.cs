using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.OpenAi;

public class OpenAiSettings : IConfigurationSetting
{
    public OpenAiSettings(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("OpenAi:ApiKey");
        Organization = configuration.GetValue<string>("OpenAi:Organization");
        
        RealtimeSendBuffLength = configuration.GetValue<int>("OpenAi:Realtime:RealtimeSendBuffLength");
        RealtimeReceiveBufferLength = configuration.GetValue<int>("OpenAi:Realtime:ReceiveBufferLength");
    }
    
    public string ApiKey { get; set; }
    
    public string Organization { get; set; }

    public int RealtimeSendBuffLength { get; set; }
    
    public int RealtimeReceiveBufferLength { get; set; }
}