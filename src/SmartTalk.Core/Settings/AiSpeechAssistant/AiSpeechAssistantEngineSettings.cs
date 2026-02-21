using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.AiSpeechAssistant;

public class AiSpeechAssistantSettings : IConfigurationSetting
{
    public AiSpeechAssistantSettings(IConfiguration configuration)
    {
        EngineVersion = configuration.GetValue<int>("AiSpeechAssistant:EngineVersion");
    }

    public int EngineVersion { get; set; }
}
