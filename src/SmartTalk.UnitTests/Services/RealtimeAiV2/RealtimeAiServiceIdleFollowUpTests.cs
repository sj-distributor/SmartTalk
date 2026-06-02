using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class RealtimeAiServiceIdleFollowUpTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task IdleFollowUp_AfterTurnCompleted_TimerStarted()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                Data = new List<RealtimeAiWssFunctionCallData>()
            });

        var options = CreateDefaultOptions(o =>
        {
            o.IdleFollowUp = new RealtimeSessionIdleFollowUp
            {
                TimeoutSeconds = 30,
                FollowUpMessage = "Are you still there?"
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        TimerManager.Received().StartTimer(
            Arg.Any<string>(),
            TimeSpan.FromSeconds(30),
            Arg.Any<Func<Task>>());
    }

    [Fact]
    public async Task IdleFollowUp_SkipRounds_TimerNotStartedOnEarlyRounds()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                Data = new List<RealtimeAiWssFunctionCallData>()
            });

        var options = CreateDefaultOptions(o =>
        {
            o.IdleFollowUp = new RealtimeSessionIdleFollowUp
            {
                TimeoutSeconds = 30,
                FollowUpMessage = "Are you still there?",
                SkipRounds = 2 // Skip first 2 rounds
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        // First turn → Round becomes 1, SkipRounds=2, 2 < 1 is false → no timer
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        TimerManager.DidNotReceive().StartTimer(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<Func<Task>>());
    }

    [Fact]
    public async Task IdleFollowUp_SkipRounds_TimerStartedAfterSkipRoundsExceeded()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                Data = new List<RealtimeAiWssFunctionCallData>()
            });

        var options = CreateDefaultOptions(o =>
        {
            o.IdleFollowUp = new RealtimeSessionIdleFollowUp
            {
                TimeoutSeconds = 30,
                FollowUpMessage = "Are you still there?",
                SkipRounds = 1 // Skip first round
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        // First turn → Round=1, SkipRounds=1, 1 < 1 is false → no timer
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(50);

        // Second turn → Round=2, SkipRounds=1, 1 < 2 is true → timer started
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        TimerManager.Received(1).StartTimer(
            Arg.Any<string>(),
            TimeSpan.FromSeconds(30),
            Arg.Any<Func<Task>>());
    }

    [Fact]
    public async Task IdleFollowUp_Null_NoTimerStarted()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                Data = new List<RealtimeAiWssFunctionCallData>()
            });

        // IdleFollowUp is null by default
        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        TimerManager.DidNotReceive().StartTimer(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<Func<Task>>());
    }

    [Fact]
    public async Task SpeechDetected_WithIdleFollowUp_StopsTimer()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.SpeechDetected
            });

        var options = CreateDefaultOptions(o =>
        {
            o.IdleFollowUp = new RealtimeSessionIdleFollowUp
            {
                TimeoutSeconds = 30,
                FollowUpMessage = "Are you still there?"
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"input_audio_buffer.speech_started\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // StopTimer called twice: once from speech handler (IdleFollowUp != null), once from cleanup
        TimerManager.Received(2).StopTimer(Arg.Any<string>());
    }

    [Fact]
    public async Task SpeechDetected_WithoutIdleFollowUp_TimerNotTouched()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.SpeechDetected
            });

        // IdleFollowUp is null by default
        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"input_audio_buffer.speech_started\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // StopTimer is called in cleanup, but NOT during the speech detected handler
        // when IdleFollowUp is null. The cleanup call always happens.
        // We verify StopTimer was called only once (from cleanup), not from speech handler
        TimerManager.Received(1).StopTimer(Arg.Any<string>());
    }

    [Fact]
    public async Task IdleFollowUp_OnTimeoutAsync_InvokedWhenTimerFires()
    {
        var actionInvoked = false;
        Func<Task> capturedCallback = null;

        TimerManager.When(x => x.StartTimer(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Func<Task>>()))
            .Do(ci => capturedCallback = ci.ArgAt<Func<Task>>(2));

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                Data = new List<RealtimeAiWssFunctionCallData>()
            });

        var options = CreateDefaultOptions(o =>
        {
            o.IdleFollowUp = new RealtimeSessionIdleFollowUp
            {
                TimeoutSeconds = 30,
                FollowUpMessage = "Are you still there?",
                OnTimeoutAsync = () => { actionInvoked = true; return Task.CompletedTask; }
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        // Simulate timer firing
        capturedCallback.ShouldNotBeNull();
        await capturedCallback!();

        actionInvoked.ShouldBeTrue();

        FakeWs.EnqueueClose();
        await sessionTask;
    }

    [Fact]
    public async Task IdleFollowUp_BothMessageAndAction_MessageSentFirst()
    {
        var order = new List<string>();
        Func<Task> capturedCallback = null;

        TimerManager.When(x => x.StartTimer(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Func<Task>>()))
            .Do(ci => capturedCallback = ci.ArgAt<Func<Task>>(2));

        ProviderAdapter.BuildTextUserMessage(Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci =>
            {
                order.Add("message");
                return $"text_user:{ci.ArgAt<string>(0)}";
            });

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                Data = new List<RealtimeAiWssFunctionCallData>()
            });

        var options = CreateDefaultOptions(o =>
        {
            o.IdleFollowUp = new RealtimeSessionIdleFollowUp
            {
                TimeoutSeconds = 30,
                FollowUpMessage = "Are you still there?",
                OnTimeoutAsync = () => { order.Add("action"); return Task.CompletedTask; }
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        capturedCallback.ShouldNotBeNull();
        await capturedCallback!();

        order.Count.ShouldBe(2);
        order[0].ShouldBe("message");
        order[1].ShouldBe("action");

        FakeWs.EnqueueClose();
        await sessionTask;
    }

    [Fact]
    public async Task IdleFollowUp_NullFollowUpMessage_OnlyActionRuns()
    {
        var actionInvoked = false;
        Func<Task> capturedCallback = null;

        TimerManager.When(x => x.StartTimer(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Func<Task>>()))
            .Do(ci => capturedCallback = ci.ArgAt<Func<Task>>(2));

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                Data = new List<RealtimeAiWssFunctionCallData>()
            });

        var options = CreateDefaultOptions(o =>
        {
            o.IdleFollowUp = new RealtimeSessionIdleFollowUp
            {
                TimeoutSeconds = 30,
                FollowUpMessage = null,
                OnTimeoutAsync = () => { actionInvoked = true; return Task.CompletedTask; }
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        capturedCallback.ShouldNotBeNull();
        await capturedCallback!();

        actionInvoked.ShouldBeTrue();
        // FollowUpMessage is null, so no text should have been sent to provider for that message
        ProviderAdapter.DidNotReceive().BuildTextUserMessage(Arg.Any<string>(), Arg.Any<string>());

        FakeWs.EnqueueClose();
        await sessionTask;
    }

    [Fact]
    public async Task IdleFollowUp_OnTimeoutAsync_ShouldBeSkipped_WhenSessionIsInactive()
    {
        var actionInvoked = false;
        Func<Task> capturedCallback = null;

        TimerManager.When(x => x.StartTimer(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Func<Task>>()))
            .Do(ci => capturedCallback = ci.ArgAt<Func<Task>>(2));

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                Data = new List<RealtimeAiWssFunctionCallData>()
            });

        var options = CreateDefaultOptions(o =>
        {
            o.IdleFollowUp = new RealtimeSessionIdleFollowUp
            {
                TimeoutSeconds = 30,
                FollowUpMessage = "Are you still there?",
                OnTimeoutAsync = () =>
                {
                    actionInvoked = true;
                    return Task.CompletedTask;
                }
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        capturedCallback.ShouldNotBeNull();
        await capturedCallback!();

        actionInvoked.ShouldBeFalse();
        ProviderAdapter.DidNotReceive().BuildTextUserMessage(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ActiveConversation_RapidTurnsWithUserInterruptions_NoSpuriousFollowUpFires()
    {
        // End-to-end integration test for the idle-follow-up behaviour at the service
        // level. The other RealtimeAiServiceIdleFollowUpTests in this file use the
        // inherited TimerManager mock and therefore don't exercise the real timer
        // cancellation path. This test bypasses the mock and constructs a separate
        // SUT with a real InactivityTimerManager so that Start/Stop ordering against
        // realistic AI-turn-completed / user-speech-detected events runs through
        // production code end-to-end.
        //
        // What this test pins (the integration-level invariant):
        //   When every AI-turn-completed event is followed by a user-speech-detected
        //   event before the idle timeout elapses, the idle follow-up callback must
        //   NEVER fire. In production this callback schedules a Hangfire
        //   HangupCallAsync job that disconnects the caller, so any leak is a
        //   call-stability bug.
        //
        // Background (the bug the InactivityTimerManager fix protects against):
        //   1. OnAiTurnCompletedAsync starts timer T1 for the idle follow-up.
        //   2. OnAiDetectedUserSpeechAsync calls StopTimer (cancels T1, removes from dict).
        //   3. Next OnAiTurnCompletedAsync starts timer T2 (puts T2 in dict).
        //   4. Pre-fix: T1's cancelled RunTimerAsync hits finally and unconditionally
        //      TryRemoves the dict entry, clobbering T2.
        //   5. T2 still runs (CTS never cancelled). Subsequent StopTimers become
        //      no-ops for it.
        //   6. After T2's timeout: OnTimeoutAsync fires → Hangfire schedules
        //      HangupCallAsync → user disconnected mid-conversation.
        //   Post-fix: the finally uses ICollection.Remove(KVP) with value-match, so
        //   T1's cleanup cannot clobber T2's registration.
        //
        // Note on scope: this test pins the integration-level invariant under
        // realistic event timings. It is not the deterministic regression trigger
        // for the specific finally-vs-StartTimer race window — that is
        // `InactivityTimerManagerTests.StartTimer_PreviousTimerFinally_ShouldNotRemoveNewTimer`,
        // which uses TaskCompletionSource-controlled callback blocking to force
        // the interleaving every run. This test catches regressions where the
        // real timer manager fails to cooperate with the service-level event
        // pipeline (e.g. StopTimer not wired to OnAiDetectedUserSpeechAsync,
        // StartTimer not wired to OnAiTurnCompletedAsync).

        var realTimerManager = new InactivityTimerManager();
        var sut = new RealtimeAiService(Switcher, realTimerManager);

        var followUpInvoked = 0;

        // Route raw WS messages to the appropriate parsed event type. The default
        // ProviderAdapter setup uses a single Returns value, but this test cycles
        // between two distinct event types, so we route per call by inspecting the
        // raw message contents.
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(ci =>
            {
                var raw = ci.ArgAt<string>(0);
                if (raw.Contains("response.done"))
                    return new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                        Data = new List<RealtimeAiWssFunctionCallData>()
                    };
                if (raw.Contains("input_audio_buffer.speech_started"))
                    return new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.SpeechDetected
                    };
                return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Unknown };
            });

        var options = CreateDefaultOptions(o =>
        {
            o.IdleFollowUp = new RealtimeSessionIdleFollowUp
            {
                // 1 second is the smallest value supported by TimeoutSeconds (int).
                // Cycle below is faster than this so a correctly-cancelling timer
                // never fires; any race-induced leak would surface as a spurious fire.
                TimeoutSeconds = 1,
                FollowUpMessage = "Are you still there?",
                OnTimeoutAsync = () =>
                {
                    Interlocked.Increment(ref followUpInvoked);
                    return Task.CompletedTask;
                }
            };
        });

        // Manually start the session against the SUT with the real timer manager.
        // (The inherited StartSessionInBackgroundAsync helper uses the base's Sut
        // which has the mock timer, so we duplicate the small amount of setup here.)
        var sessionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionTask = Task.Run(async () =>
        {
            try
            {
                _ = Task.Delay(50).ContinueWith(_ => sessionStarted.TrySetResult());
                await sut.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { sessionStarted.TrySetException(ex); }
        });
        await sessionStarted.Task.ConfigureAwait(false);
        await Task.Delay(50).ConfigureAwait(false);

        // Simulate a rapid 30-cycle conversation: AI-turn-completed → user-speech.
        // Each AI-turn-completed starts the timer (1s timeout). Each user-speech
        // stops it. The 30ms gap between events is far smaller than the 1s timeout,
        // so a correctly-functioning timer never fires. If the race were present,
        // the cumulative effect of 30 Start/Stop pairs would leak at least one
        // entry into the "running but not in dict" state, and that entry would
        // fire its callback ~1s later.
        for (var i = 0; i < 30; i++)
        {
            await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
            await Task.Delay(15).ConfigureAwait(false);
            await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"input_audio_buffer.speech_started\"}");
            await Task.Delay(15).ConfigureAwait(false);
        }

        // Final cycle: simulate a final AI-turn-completed (timer starts) followed
        // immediately by closing the WebSocket. Cleanup must cancel the timer; no
        // follow-up should fire during cleanup.
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(50).ConfigureAwait(false);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Wait beyond the 1s timeout. Any leaked entry would fire by now.
        await Task.Delay(1500).ConfigureAwait(false);

        followUpInvoked.ShouldBe(0,
            "Idle follow-up must never fire during an active conversation where every AI turn is followed by a user-speech-detected event");
    }
}
