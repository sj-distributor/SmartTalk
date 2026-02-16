using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Settings.AiSpeechAssistant;

public class AiSpeechAssistantEngineSettings : ISingletonDependency
{
    public bool UseV2Engine { get; set; }
}
