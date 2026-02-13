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
}
