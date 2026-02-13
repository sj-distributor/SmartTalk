using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
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
}
