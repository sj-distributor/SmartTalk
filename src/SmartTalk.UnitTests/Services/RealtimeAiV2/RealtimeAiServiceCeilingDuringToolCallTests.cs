using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The turn hard ceiling measures wall time from the moment a response starts. Function-call handlers
/// run INLINE on the provider receive loop, and the turn is only stamped complete in the finally that
/// runs after every handler returns — so the ceiling's clock covers the engine's own tool execution,
/// which the blocked receive loop cannot hold back.
///
/// <para>That is not theoretical on this chain. repeat_order suspends the caller's audio, packages the
/// whole call as a WAV, uploads it for a spoken readback and shells ffmpeg, all uncancellable; one
/// retried attempt already outlives the ceiling. Forcing the turn there does three things to a call
/// that is working: it clears the two fields barge-in needs, so the assistant talks over the caller for
/// the rest of the turn; it advances Round, so the idle follow-up and auto-hangup arm a turn early; and
/// it starts the inactivity timer, whose default handling schedules a job that HANGS UP the call.</para>
///
/// <para>The bound has to mean "the provider went quiet", never "our own tool is still working".</para>
/// </summary>
public class RealtimeAiServiceCeilingDuringToolCallTests : RealtimeAiServiceTestBase
{
    private static readonly TimeSpan ShortCeiling = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan PastCeiling = TimeSpan.FromMilliseconds(500);

    private void ProviderStartsATurnThenCallsATool()
    {
        ProviderAdapter.ParseMessage("started").Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted });
        ProviderAdapter.ParseMessage("fc").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.FunctionCallSuggested,
            Data = new List<RealtimeAiWssFunctionCallData> { new() { FunctionName = "repeat_order", CallId = "call_1" } }
        });
    }

    private int TurnCompletedFrames() => FakeWs.GetSentTextMessages().Count(m => m.Contains("AiTurnCompleted"));

    [Fact]
    public async Task AToolHandlerThatOutlivesTheCeiling_ShouldNotHaveItsTurnForceCompleted()
    {
        ProviderStartsATurnThenCallsATool();
        Sut.TurnHardCeilingWatchdogOverride = ShortCeiling;

        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = CreateDefaultOptions(o => o.OnFunctionCallAsync = async (_, _) =>
        {
            await releaseHandler.Task;
            return new RealtimeAiFunctionCallResult { Output = "ok" };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("started");

        // Not awaited: the handler blocks the receive loop, which is precisely the condition under test.
        _ = FakeWssClient.SimulateMessageReceivedAsync("fc");

        await Task.Delay(PastCeiling);

        TurnCompletedFrames().ShouldBe(0, "the provider is not silent — the engine's own tool is still running");
        TimerManager.DidNotReceive().StartTimer(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Func<Task>>());

        releaseHandler.SetResult();
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AProviderThatGoesQuietWithNoToolRunning_ShouldStillBeForceCompleted()
    {
        // The other half: suspending the clock for our own work must not disarm the bound itself.
        ProviderStartsATurnThenCallsATool();
        Sut.TurnHardCeilingWatchdogOverride = ShortCeiling;

        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await Task.Delay(PastCeiling);

        TurnCompletedFrames().ShouldBe(1, "a response that started and then went quiet is what the ceiling is for");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InAudioMode_ProviderTextDone_ShouldNotArmACeiling()
    {
        // The OpenAI adapter routes an audio part's transcript to ResponseTextDone, so this arrives on
        // ordinary production turns. Arming here put a second ceiling on every one of them, and — worse
        // — made a ceiling reachable on providers that never announce a response at all.
        ProviderAdapter.ParseMessage("text-done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTextDone,
            Data = new RealtimeAiWssTextData { Text = "your order is ready" }
        });
        Sut.TurnHardCeilingWatchdogOverride = ShortCeiling;

        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("text-done");
        await Task.Delay(PastCeiling);

        TurnCompletedFrames().ShouldBe(0, "audio mode completes on provider-done; nothing here should arm a ceiling");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AProviderThatNeverAnnouncesAResponse_ShouldKeepCompletingEveryTurn()
    {
        // The Google shape: its adapter emits audio deltas and turn-complete but never ResponseStarted,
        // which is the only thing that advances the turn generation. A ceiling firing on such a session
        // would stamp a generation that never changes again, and the guard reading that stamp would
        // silently swallow every later turn completion for the rest of the call. Pinned so the trap
        // cannot be reopened by arming a ceiling on that path.
        ProviderAdapter.ParseMessage("turn-complete").Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseTurnCompleted });
        Sut.TurnHardCeilingWatchdogOverride = ShortCeiling;

        var sessionTask = await StartSessionInBackgroundAsync();

        for (var turn = 0; turn < 3; turn++)
        {
            await FakeWssClient.SimulateMessageReceivedAsync("turn-complete");
            await Task.Delay(PastCeiling);
        }

        TurnCompletedFrames().ShouldBe(3, "every turn must still complete after any earlier forced completion");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AToolThatFinishesBeforeTheCeiling_ShouldCompleteItsTurnExactlyOnce()
    {
        // The healthy path must stay byte-identical: one completion, from the normal gate.
        ProviderStartsATurnThenCallsATool();
        Sut.TurnHardCeilingWatchdogOverride = ShortCeiling;

        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await FakeWssClient.SimulateMessageReceivedAsync("fc");
        await Task.Delay(PastCeiling);

        TurnCompletedFrames().ShouldBe(1);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
