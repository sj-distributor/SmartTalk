using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Tool handlers run inline on the provider receive loop and nothing bounds them. The turn ceiling
/// deliberately stands down while one runs, because firing through a running handler force-completed a
/// healthy turn and ended in a scheduled hangup.
///
/// <para>A bound is not the answer, and this suite exists partly to keep the next reader from reaching
/// for one. Bounding the WAIT without bounding the WORK is worse than the unbounded handler: the slow
/// tool suspends the caller's microphone as its first statement and only its own finally clears that
/// flag, so abandoning it leaves the caller mute while the turn completes and the idle timer schedules
/// a hangup — dropping a customer who is still talking.</para>
///
/// <para>So this records instead. The completion line only ever reports handlers that RETURNED, which
/// means the one that wedges is precisely the one nothing measures. These make it measurable, and fence
/// the observation as inert.</para>
/// </summary>
public class RealtimeAiServiceSlowFunctionCallObservationTests : RealtimeAiServiceTestBase
{
    private void ProviderStartsATurnThenCallsATool()
    {
        ProviderAdapter.ParseMessage("started").Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted });
        ProviderAdapter.ParseMessage("fc").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.FunctionCallSuggested,
            Data = new List<RealtimeAiWssFunctionCallData> { new() { FunctionName = "repeat_order", CallId = "call_1" } }
        });
    }

    private void UseFastObserver()
    {
        Sut.FunctionCallStillRunningThresholdOverride = TimeSpan.FromMilliseconds(100);
        Sut.ProviderLivenessPollIntervalOverride = TimeSpan.FromMilliseconds(20);
        Sut.TurnHardCeilingWatchdogOverride = TimeSpan.FromMilliseconds(120);
    }

    private RealtimeSessionOptions BlockingHandlerOptions(TaskCompletionSource release) =>
        CreateDefaultOptions(o => o.OnFunctionCallAsync = async (_, _) =>
        {
            await release.Task;
            return new RealtimeAiFunctionCallResult { Output = "ok" };
        });

    private int TurnCompletedFrames() => FakeWs.GetSentTextMessages().Count(m => m.Contains("AiTurnCompleted"));

    private static LogEvent Observation(string fragment) =>
        TestCorrelator.GetLogEventsFromCurrentContext().Single(e => e.MessageTemplate.Text.Contains(fragment));

    [Fact]
    public async Task AHandlerStillHoldingTheLoop_ShouldBeRecordedAndOtherwiseLeaveTheTurnAlone()
    {
        using var correlator = TestCorrelator.CreateContext();
        UseFastObserver();
        ProviderStartsATurnThenCallsATool();

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionTask = await StartSessionInBackgroundAsync(BlockingHandlerOptions(release));

        await FakeWssClient.SimulateMessageReceivedAsync("started");

        // Not awaited: the handler holds the receive loop, which is the condition under test.
        _ = FakeWssClient.SimulateMessageReceivedAsync("fc");

        await Task.Delay(500);

        var observation = Observation("still holding the receive loop");

        observation.Level.ShouldBe(LogEventLevel.Warning);
        observation.Properties["FunctionName"].ToString().ShouldContain("repeat_order");
        double.Parse(observation.Properties["ElapsedFunctionCallMs"].ToString()).ShouldBeGreaterThanOrEqualTo(100);

        // The fence, and the reason this is the right first test rather than "a slow handler logs a
        // warning": observing must stay inert. An implementation that quietly disturbs the ceiling's
        // stand-down would satisfy the log assertion and reopen the hangup.
        TurnCompletedFrames().ShouldBe(0, "observing must not complete a turn the engine is still serving");
        TimerManager.DidNotReceive().StartTimer(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Func<Task>>());
        ClientAdapter.DidNotReceive().BuildErrorMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

        release.SetResult();
        await Task.Delay(150);

        FakeWssClient.SentMessages.Any(m => m.Contains("fc_reply")).ShouldBeTrue("the handler's reply must still reach the provider");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AHandlerStillWedgedWhenTheCallerHangsUp_ShouldStillHaveItsDurationRecorded()
    {
        // The tail, and the whole point. A handler that never returns never reaches the completion
        // line, so without a final observation the one run worth measuring leaves no duration at all.
        using var correlator = TestCorrelator.CreateContext();
        UseFastObserver();
        ProviderStartsATurnThenCallsATool();

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionTask = await StartSessionInBackgroundAsync(BlockingHandlerOptions(release));

        await FakeWssClient.SimulateMessageReceivedAsync("started");
        _ = FakeWssClient.SimulateMessageReceivedAsync("fc");

        await Task.Delay(300);

        FakeWs.EnqueueClose();
        await Task.Delay(300);

        var final = Observation("Session ended with a function call still running");

        final.Level.ShouldBe(LogEventLevel.Warning);
        final.Properties["FunctionName"].ToString().ShouldContain("repeat_order");
        double.Parse(final.Properties["ElapsedFunctionCallMs"].ToString()).ShouldBeGreaterThanOrEqualTo(200);

        release.SetResult();
        await Task.Delay(100);
    }

    [Fact]
    public async Task AHandlerThatReturnsQuickly_ShouldNotBeRecorded()
    {
        // The healthy path stays silent. A line here on every tool call would bury the one that matters.
        using var correlator = TestCorrelator.CreateContext();
        UseFastObserver();
        ProviderStartsATurnThenCallsATool();

        var options = CreateDefaultOptions(o => o.OnFunctionCallAsync = (_, _) =>
            Task.FromResult(new RealtimeAiFunctionCallResult { Output = "ok" }));

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await FakeWssClient.SimulateMessageReceivedAsync("fc");
        await Task.Delay(300);

        TestCorrelator.GetLogEventsFromCurrentContext()
            .ShouldNotContain(e => e.MessageTemplate.Text.Contains("still holding the receive loop"));

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TheObservationThreshold_ShouldMatchTheCeilingItStandsDownFor()
    {
        // Legible together on purpose: the moment the ceiling stands down for a running tool is the
        // moment that tool becomes worth recording. A log threshold, never a bound — nothing acts on it.
        SmartTalk.Core.Services.RealtimeAiV2.Liveness.RealtimeAiLivenessDefaults.FunctionCallStillRunning
            .ShouldBe(SmartTalk.Core.Services.RealtimeAiV2.Watchdog.RealtimeAiTurnWatchdogDefaults.TurnHardCeiling);
    }
}
