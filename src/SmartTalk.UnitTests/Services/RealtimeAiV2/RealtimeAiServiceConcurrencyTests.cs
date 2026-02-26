using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.UnitTests.Services.RealtimeAiV2.Fakes;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class RealtimeAiServiceConcurrencyTests
{
    [Fact]
    public async Task TwoConcurrentSessions_OperateIndependently()
    {
        // Session 1 setup
        var fakeWs1 = new FakeWebSocket();
        var fakeWssClient1 = new FakeRealtimeAiWssClient();
        var providerAdapter1 = Substitute.For<IRealtimeAiProviderAdapter>();
        var clientAdapter1 = Substitute.For<IRealtimeAiClientAdapter>();
        var switcher1 = Substitute.For<IRealtimeAiSwitcher>();
        var timerManager1 = Substitute.For<IInactivityTimerManager>();

        switcher1.WssClient(Arg.Any<RealtimeAiProvider>()).Returns(fakeWssClient1);
        switcher1.ClientAdapter(Arg.Any<RealtimeAiClient>()).Returns(clientAdapter1);
        switcher1.ProviderAdapter(Arg.Any<RealtimeAiProvider>()).Returns(providerAdapter1);
        providerAdapter1.GetHeaders(Arg.Any<RealtimeAiServerRegion>()).Returns(new Dictionary<string, string>());
        providerAdapter1.BuildSessionConfig(Arg.Any<RealtimeSessionOptions>(), Arg.Any<RealtimeAiAudioCodec>()).Returns(new { });

        // Session 2 setup
        var fakeWs2 = new FakeWebSocket();
        var fakeWssClient2 = new FakeRealtimeAiWssClient();
        var providerAdapter2 = Substitute.For<IRealtimeAiProviderAdapter>();
        var clientAdapter2 = Substitute.For<IRealtimeAiClientAdapter>();
        var switcher2 = Substitute.For<IRealtimeAiSwitcher>();
        var timerManager2 = Substitute.For<IInactivityTimerManager>();

        switcher2.WssClient(Arg.Any<RealtimeAiProvider>()).Returns(fakeWssClient2);
        switcher2.ClientAdapter(Arg.Any<RealtimeAiClient>()).Returns(clientAdapter2);
        switcher2.ProviderAdapter(Arg.Any<RealtimeAiProvider>()).Returns(providerAdapter2);
        providerAdapter2.GetHeaders(Arg.Any<RealtimeAiServerRegion>()).Returns(new Dictionary<string, string>());
        providerAdapter2.BuildSessionConfig(Arg.Any<RealtimeSessionOptions>(), Arg.Any<RealtimeAiAudioCodec>()).Returns(new { });

        var sut1 = new RealtimeAiService(switcher1, timerManager1);
        var sut2 = new RealtimeAiService(switcher2, timerManager2);

        string? session1Id = null;
        string? session2Id = null;

        var options1 = new RealtimeSessionOptions
        {
            WebSocket = fakeWs1,
            ClientConfig = new RealtimeAiClientConfig { Client = RealtimeAiClient.Default },
            ModelConfig = new RealtimeAiModelConfig
            {
                Provider = RealtimeAiProvider.OpenAi,
                ServiceUrl = "wss://api.openai.com/v1/realtime",
            },
            ConnectionProfile = new RealtimeAiConnectionProfile { ProfileId = "p1" },
            OnSessionEndedAsync = id => { session1Id = id; return Task.CompletedTask; }
        };

        var options2 = new RealtimeSessionOptions
        {
            WebSocket = fakeWs2,
            ClientConfig = new RealtimeAiClientConfig { Client = RealtimeAiClient.Default },
            ModelConfig = new RealtimeAiModelConfig
            {
                Provider = RealtimeAiProvider.OpenAi,
                ServiceUrl = "wss://api.openai.com/v1/realtime",
            },
            ConnectionProfile = new RealtimeAiConnectionProfile { ProfileId = "p2" },
            OnSessionEndedAsync = id => { session2Id = id; return Task.CompletedTask; }
        };

        // Start both sessions concurrently
        var task1 = Task.Run(async () =>
        {
            await Task.Delay(20);
            fakeWs1.EnqueueClose();
        });
        var task2 = Task.Run(async () =>
        {
            await Task.Delay(20);
            fakeWs2.EnqueueClose();
        });

        var sessionTask1 = sut1.ConnectAsync(options1, CancellationToken.None);
        var sessionTask2 = sut2.ConnectAsync(options2, CancellationToken.None);

        await Task.WhenAll(sessionTask1, sessionTask2, task1, task2);

        // Both sessions should have unique IDs
        session1Id.ShouldNotBeNullOrEmpty();
        session2Id.ShouldNotBeNullOrEmpty();
        session1Id.ShouldNotBe(session2Id);

        // Each WssClient should have been connected independently
        fakeWssClient1.ConnectCallCount.ShouldBe(1);
        fakeWssClient2.ConnectCallCount.ShouldBe(1);
    }
}
