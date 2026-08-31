using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.AiSpeechAssistant;

public class AiSpeechAssistantSettings : IConfigurationSetting
{
    public AiSpeechAssistantSettings(IConfiguration configuration)
    {
        EngineVersion = configuration.GetValue<int>("AiSpeechAssistant:EngineVersion");
        SessionCredentialLifetimeMinutes = configuration.GetValue<int>(
            "AiSpeechAssistant:SessionCredentialLifetimeMinutes",
            24 * 60);
    }

    public int EngineVersion { get; set; }

    public int SessionCredentialLifetimeMinutes { get; set; }
}
