using System.Net.WebSockets;
using Autofac;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
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
    })
    {
    }

    protected record ProviderMock(
        Action<string> EnqueueMessage,
        List<byte[]> SentMessages,
        Action<ContainerBuilder> Register);

    protected static ProviderMock CreateProviderMock()
    {
        if (UseV2Engine)
        {
            var mock = new MockRealtimeAiWssClient();
            return new ProviderMock(mock.EnqueueMessage, mock.SentMessages,
                builder => RegisterMockProvider(builder, mock));
        }
        else
        {
            var mock = new MockWebSocket(waitForCloseSignal: true);
            return new ProviderMock(mock.EnqueueMessage, mock.SentMessages,
                builder => builder.RegisterInstance(mock).As<WebSocket>());
        }
    }

    private static void RegisterMockProvider(ContainerBuilder builder, MockRealtimeAiWssClient mock)
    {
        builder.Register(ctx => new RealtimeAiSwitcher(
            [mock],
            ctx.Resolve<IEnumerable<IRealtimeAiClientAdapter>>(),
            ctx.Resolve<IEnumerable<IRealtimeAiProviderAdapter>>()
        )).As<IRealtimeAiSwitcher>();
    }
}
