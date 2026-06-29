using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the engine's error-routing FORK in OnProviderErrorAsync:
/// an "active response in progress" conflict is QUEUED as a retry (no client notification, no
/// disconnect), whereas any other critical error notifies the client and disconnects.
///
/// The existing ErrorHandlingTests cover the generic critical-disconnect path but not the
/// recoverable active-response retry branch. Migration step S3 (route on Kind) and S11 (error
/// taxonomy) rewrite this fork; it must survive, or these tests fail RED.
/// </summary>
public class RealtimeAiServiceRecoverableErrorRoutingGoldenTests : RealtimeAiServiceTestBase
{
    private static ParsedRealtimeAiProviderEvent Error(string code, string message, bool isCritical) =>
        new()
        {
            Type = RealtimeAiWssEventType.Error,
            Data = new RealtimeAiErrorData { Code = code, Message = message, IsCritical = isCritical }
        };

    private static ParsedRealtimeAiProviderEvent TurnCompleted() =>
        new() { Type = RealtimeAiWssEventType.ResponseTurnCompleted, Data = new List<RealtimeAiWssFunctionCallData>() };

    [Theory]
    // recoverable by code
    [InlineData("conversation_already_has_active_response", "busy")]
    // recoverable by message substring (case-insensitive)
    [InlineData("server_error", "There is an active response in progress, retry")]
    public async Task ActiveResponseInProgressError_QueuesRetry_NoClientNotifyNoDisconnect_DrainsOnTurnComplete(string code, string message)
    {
        var call = 0;
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(_ => ++call == 1 ? Error(code, message, isCritical: false) : TurnCompleted());

        var sessionTask = await StartSessionInBackgroundAsync();

        // Event 1: the active-response conflict — queued as a retry, not surfaced.
        await FakeWssClient.SimulateMessageReceivedAsync("{}");
        await Task.Delay(50);

        ClientAdapter.DidNotReceive().BuildErrorMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        FakeWssClient.DisconnectCallCount.ShouldBe(0);

        // Event 2: a turn-complete drains the queued retry → exactly one response trigger is sent,
        // proving the retry was queued (not dropped) and the session stayed alive.
        await FakeWssClient.SimulateMessageReceivedAsync("{}");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask;

        ClientAdapter.Received().BuildTurnCompletedMessage(Arg.Any<string>());
        FakeWssClient.SentMessages.Count(m => m == "response_create_msg").ShouldBe(1);
    }

    [Fact]
    public async Task CriticalNonActiveResponseError_NotifiesClientAndDisconnects()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(Error("server_error", "boom", isCritical: true));

        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("{}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        ClientAdapter.Received().BuildErrorMessage("server_error", "boom", Arg.Any<string>());
        FakeWssClient.DisconnectCallCount.ShouldBeGreaterThan(0);
    }
}
