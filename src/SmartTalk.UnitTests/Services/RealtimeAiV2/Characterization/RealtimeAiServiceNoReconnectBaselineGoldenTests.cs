using System.Net.WebSockets;
using NSubstitute;
using Shouldly;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the pre-S12 "no auto-reconnect" baseline: when the provider socket
/// transitions to Closed/Aborted while the session is active, the engine raises a critical ConnectionLost
/// error, notifies the client, disconnects ONCE, and does NOT attempt to re-establish the provider
/// connection. Migration step S12 introduces a ConnectionController with reconnect/heartbeat; locking
/// the current single-disconnect/no-reconnect behavior makes that a deliberate, asserted change.
/// </summary>
public class RealtimeAiServiceNoReconnectBaselineGoldenTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task ProviderClosedWhileActive_CriticalConnectionLost_NoAutoReconnect()
    {
        string? endedSessionId = null;
        var options = CreateDefaultOptions(o => o.OnSessionEndedAsync = id => { endedSessionId = id; return Task.CompletedTask; });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        FakeWssClient.ConnectCallCount.ShouldBe(1);   // connected exactly once at session start

        await FakeWssClient.SimulateStateChangedAsync(WebSocketState.Closed, "server closed");
        await Task.Delay(150);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Critical ConnectionLost notified to the client, session torn down, and the provider is NOT
        // re-connected (pre-S12 baseline). DisconnectAsync stays at 0 because the socket already reported
        // Closed, so DisconnectFromProviderAsync skips the redundant close — itself a pinned behavior.
        ClientAdapter.Received().BuildErrorMessage("ConnectionLost", Arg.Is<string>(s => s.Contains("server closed")), Arg.Any<string>());
        FakeWssClient.ConnectCallCount.ShouldBe(1);
        FakeWssClient.DisconnectCallCount.ShouldBe(0);
        endedSessionId.ShouldNotBeNullOrEmpty();
    }
}
