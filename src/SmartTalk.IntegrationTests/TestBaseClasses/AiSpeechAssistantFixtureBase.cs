using Xunit;

namespace SmartTalk.IntegrationTests.TestBaseClasses;

[Collection("AiSpeechAssistant Tests")]
public class AiSpeechAssistantFixtureBase : TestBase
{
    protected AiSpeechAssistantFixtureBase() : base("_ai_speech_assistant_", "ai_speech_assistant", 2)
    {
    }
}
