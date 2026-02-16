using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public class AiSpeechAssistantEngineSettings : ISingletonDependency
{
    public bool UseV2Engine { get; set; }
}
