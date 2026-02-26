using System.Net.WebSockets;
using Autofac;
using Microsoft.Extensions.Configuration;
using SmartTalk.Core.Settings.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.IntegrationTests.Mocks;
using Xunit;

namespace SmartTalk.IntegrationTests.TestBaseClasses;

[Collection("AiSpeechAssistant Tests")]
public class AiSpeechAssistantFixtureBase : TestBase
{
    protected const int EngineVersion = 2;

    protected AiSpeechAssistantFixtureBase() : base("_ai_speech_assistant_", "smart_talk_ai_speech_assistant", 2, builder =>
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["AiSpeechAssistant:EngineVersion"] = EngineVersion.ToString()
            })
            .Build();

        builder.RegisterInstance(new AiSpeechAssistantSettings(config));
    })
    {
    }

    protected record ProviderMock(
        Action<string> EnqueueMessage,
        Action<string> EnqueueSendTriggeredMessage,
        List<byte[]> SentMessages,
        Action<ContainerBuilder> Register);

    protected static ProviderMock CreateProviderMock()
    {
        if (EngineVersion == 2)
        {
            var mock = new MockRealtimeAiWssClient();
            return new ProviderMock(mock.EnqueueMessage, mock.EnqueueSendTriggeredMessage, mock.SentMessages,
                builder => RegisterMockProvider(builder, mock));
        }
        else
        {
            var mock = new MockWebSocket(waitForCloseSignal: true);
            return new ProviderMock(mock.EnqueueMessage, mock.EnqueueMessage, mock.SentMessages,
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
