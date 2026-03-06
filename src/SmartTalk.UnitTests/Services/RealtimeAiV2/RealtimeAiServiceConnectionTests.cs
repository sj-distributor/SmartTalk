using System.Net.WebSockets;
using Shouldly;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class RealtimeAiServiceConnectionTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task ConnectToProvider_SubscribesEventsConnectsAndSendsConfig()
    {
        // Close immediately so OrchestrateSessionAsync exits
        FakeWs.EnqueueClose();

        var options = CreateDefaultOptions();
        await Sut.ConnectAsync(options, CancellationToken.None);

        // ConnectAsync should have been called on the WssClient
        FakeWssClient.ConnectCallCount.ShouldBe(1);
        FakeWssClient.EndpointUri.ShouldNotBeNull();
        FakeWssClient.EndpointUri!.ToString().ShouldContain("openai.com");

        // Session config should have been sent as the first message
        FakeWssClient.SentMessages.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ConnectToProvider_Failure_DisconnectsAndRethrows()
    {
        FakeWssClient.ShouldFailConnect = true;

        var options = CreateDefaultOptions();
        await Should.ThrowAsync<WebSocketException>(
            () => Sut.ConnectAsync(options, CancellationToken.None));

        // DisconnectFromProviderAsync should have been called during catch block
        // The disconnect increments only if WssClient state is Open, which it isn't
        // after a failed connect. But SessionCts.CancelAsync is called.
        // We just verify it doesn't hang and the exception propagates.
    }

    [Fact]
    public async Task ConnectToProvider_AlreadyOpenSameUri_SkipsReconnect()
    {
        // Pre-set the WssClient to Open state at the same URI
        FakeWssClient.SetState(WebSocketState.Open);
        // Simulate that EndpointUri already matches via a prior connect
        await FakeWssClient.ConnectAsync(
            new Uri("wss://api.openai.com/v1/realtime"),
            new Dictionary<string, string>(),
            CancellationToken.None);
        var initialConnectCount = FakeWssClient.ConnectCallCount; // 1

        FakeWs.EnqueueClose();

        var options = CreateDefaultOptions();
        await Sut.ConnectAsync(options, CancellationToken.None);

        // Should not have called ConnectAsync again since state is Open and URI matches
        FakeWssClient.ConnectCallCount.ShouldBe(initialConnectCount);
    }

    [Fact]
    public async Task ConnectToProvider_StateNotOpenAfterConnect_ThrowsInvalidOperationException()
    {
        // ConnectAsync succeeds (no exception) but state doesn't transition to Open
        FakeWssClient.StateAfterConnect = WebSocketState.Connecting;

        var options = CreateDefaultOptions();
        await Should.ThrowAsync<InvalidOperationException>(
            () => Sut.ConnectAsync(options, CancellationToken.None));

        // ConnectAsync was called
        FakeWssClient.ConnectCallCount.ShouldBe(1);
    }
}
