using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the idle-follow-up timer callback's behavior when it fires after the
/// session is no longer active (the stale-_ctx-closure scenario S12's reconnect/CTS-replacement will
/// stress): both the follow-up message send and the OnTimeoutAsync action are skipped because
/// IsProviderSessionActive is false. The existing IdleFollowUp tests never fire the callback across a
/// cancelled session.
/// </summary>
public class RealtimeAiServiceIdleFollowUpClosureGoldenTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task IdleCallback_FiringAfterSessionInactive_SkipsFollowUpAndOnTimeout()
    {
        Func<Task>? idleCallback = null;
        TimerManager.When(t => t.StartTimer(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Func<Task>>()))
            .Do(ci => idleCallback = ci.ArgAt<Func<Task>>(2));

        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTurnCompleted,
            Data = new List<RealtimeAiWssFunctionCallData>()
        });

        var onTimeoutCalled = false;
        var options = CreateDefaultOptions(o => o.IdleFollowUp = new RealtimeSessionIdleFollowUp
        {
            TimeoutSeconds = 60,
            FollowUpMessage = "still there?",
            OnTimeoutAsync = () => { onTimeoutCalled = true; return Task.CompletedTask; }
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("done");   // OnAiTurnCompleted → StartTimer captures the callback
        idleCallback.ShouldNotBeNull();

        FakeWs.EnqueueClose();
        await sessionTask;                                          // session ends → SessionCts cancelled

        await idleCallback!();                                      // the timer fires after the session is gone

        ProviderAdapter.DidNotReceive().BuildTextUserMessage("still there?", Arg.Any<string>());
        onTimeoutCalled.ShouldBeFalse();
    }
}
