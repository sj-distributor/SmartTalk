using Autofac;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.IntegrationTests.Mocks;
using Xunit;

namespace SmartTalk.IntegrationTests.TestBaseClasses;

[Collection("AiSpeechAssistant Tests")]
public class AiSpeechAssistantFixtureBase : TestBase
{
    protected const bool UseV2Engine = true;

    protected AiSpeechAssistantFixtureBase() : base("_ai_speech_assistant_", "ai_speech_assistant", 2, builder =>
    {
        builder.RegisterInstance(new AiSpeechAssistantEngineSettings { UseV2Engine = UseV2Engine });
        builder.RegisterType<BridgeRealtimeAiService>().As<IRealtimeAiService>().InstancePerLifetimeScope();
    })
    {
    }
}
