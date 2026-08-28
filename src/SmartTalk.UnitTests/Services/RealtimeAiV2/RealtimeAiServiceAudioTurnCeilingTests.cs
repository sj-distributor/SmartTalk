using NSubstitute;
using Shouldly;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Until now both turn watchdogs armed only on the external-TTS path, which is gated to a single
/// assistant and ships disabled. The built-in audio path — every phone call — had no turn-level
/// bound at all, so a provider that goes quiet mid-response leaves the turn open forever: no
/// completion, no idle timer, nothing to move the call on, and the caller listening to silence on a
/// live billed line until TCP finally gives up around fifteen minutes later.
///
/// <para>The ceiling is deliberately far above any plausible turn rather than tuned to observed
/// latency. Two minutes cannot be reached by a legitimate response, so arming it needs no production
/// distribution to justify — it can only fire on a turn that is already broken. A tighter bound is
/// worth having later, from measurements rather than from a guess.</para>
/// </summary>
public class RealtimeAiServiceAudioTurnCeilingTests : RealtimeAiServiceTestBase
{
    private void UseShortCeiling() => Sut.TurnHardCeilingWatchdogOverride = TimeSpan.FromMilliseconds(150);

    private void ProviderEvents() =>
        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0) switch
        {
            "started" => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted },
            _ => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseTurnCompleted }
        });

    [Fact]
    public async Task AudioTurnThatNeverCompletes_ShouldBeForcedClosedAtTheCeiling()
    {
        UseShortCeiling();
        ProviderEvents();

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("started");

        // The provider then goes quiet: no response.done ever arrives.
        await Task.Delay(400);

        ClientAdapter.Received(1).BuildTurnCompletedMessage(Arg.Any<string>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ForcedTurn_ShouldStillArmTheIdleFollowUp()
    {
        // The point of completing it: without this the call has nothing to move it forward.
        UseShortCeiling();
        ProviderEvents();

        var options = CreateDefaultOptions(o => o.IdleFollowUp =
            new SmartTalk.Core.Services.RealtimeAiV2.RealtimeSessionIdleFollowUp { TimeoutSeconds = 30, FollowUpMessage = "still there?" });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await Task.Delay(400);

        TimerManager.Received().StartTimer(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Func<Task>>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TurnThatCompletesNormally_ShouldNotBeCompletedTwiceByTheCeiling()
    {
        // The risk the idempotence flag exists for: the ceiling must not add a second completion
        // behind a turn that already finished on its own.
        UseShortCeiling();
        ProviderEvents();

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await FakeWssClient.SimulateMessageReceivedAsync("done");
        await Task.Delay(400);

        ClientAdapter.Received(1).BuildTurnCompletedMessage(Arg.Any<string>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LateProviderDone_AfterAForcedCompletion_ShouldNotCompleteAgain()
    {
        // The other half: the provider recovers and sends response.done after the ceiling already
        // closed the turn. Round would otherwise jump by two, and SkipRounds drives when the idle
        // follow-up and auto-hangup fire.
        UseShortCeiling();
        ProviderEvents();

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await Task.Delay(400);

        await FakeWssClient.SimulateMessageReceivedAsync("done");
        await Task.Delay(50);

        ClientAdapter.Received(1).BuildTurnCompletedMessage(Arg.Any<string>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SuccessiveAudioTurns_ShouldEachCompleteOnce()
    {
        // A superseded turn's ceiling must not close the turn that replaced it.
        Sut.TurnHardCeilingWatchdogOverride = TimeSpan.FromSeconds(30);
        ProviderEvents();

        var sessionTask = await StartSessionInBackgroundAsync();

        for (var turn = 0; turn < 3; turn++)
        {
            await FakeWssClient.SimulateMessageReceivedAsync("started");
            await FakeWssClient.SimulateMessageReceivedAsync("done");
            await Task.Delay(20);
        }

        ClientAdapter.Received(3).BuildTurnCompletedMessage(Arg.Any<string>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
