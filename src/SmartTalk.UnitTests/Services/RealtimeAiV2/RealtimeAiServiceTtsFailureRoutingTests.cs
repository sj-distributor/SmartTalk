using NSubstitute;
using SmartTalk.Core.Services.RealtimeAiV2;
using Shouldly;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.UnitTests.Services.RealtimeAiV2.Fakes;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// CHARACTERIZATION — how the engine routes a TTS provider's terminal FAILURE signal.
///
/// <para>This path had no coverage at all, and not by oversight: every existing TTS double is
/// <c>BuiltInRealtimeAiTtsProvider</c>, whose <c>SynthesisFailedAsync</c> has empty add/remove
/// accessors and silently drops every subscription. <c>OnTtsSynthesisFailedAsync</c>
/// (RealtimeAiService.Event.cs:190-201) was structurally unreachable from the suite — while being
/// able to send an error frame to the client and, on a critical error, tear down a live call.</para>
///
/// <para>The severity flag is the whole behaviour here: it decides between "notify and keep the
/// caller connected" and "hang up". The hardening plan's error-taxonomy work (P3.3) rewrites that
/// decision, so the current routing needs to be pinned before it moves.</para>
/// </summary>
public class RealtimeAiServiceTtsFailureRoutingTests : RealtimeAiServiceTestBase
{
    private readonly FakeFailingTtsProvider _tts = new();

    private RealtimeSessionOptions UseFailingTts()
    {
        Switcher.TtsProvider(Arg.Any<RealtimeAiTtsProviderType>()).Returns(_tts);
        return CreateDefaultOptions();
    }

    [Fact]
    public async Task NonCriticalSynthesisFailure_NotifiesClientAndKeepsSessionAlive()
    {
        var sessionTask = await StartSessionInBackgroundAsync(UseFailingTts());

        await _tts.SimulateSynthesisFailedAsync(new RealtimeAiErrorData
        {
            Code = "voice_unavailable", Message = "temporary synthesis failure", IsCritical = false
        });
        await Task.Delay(50);

        ClientAdapter.Received().BuildErrorMessage("voice_unavailable", "temporary synthesis failure", Arg.Any<string>());
        FakeWssClient.DisconnectCallCount.ShouldBe(0, "a recoverable synthesis failure must not hang up on the caller");
        sessionTask.IsCompleted.ShouldBeFalse();

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CriticalSynthesisFailure_TearsDownTheSession()
    {
        var sessionTask = await StartSessionInBackgroundAsync(UseFailingTts());

        await _tts.SimulateSynthesisFailedAsync(new RealtimeAiErrorData
        {
            Code = "tts_auth_failed", Message = "credentials rejected", IsCritical = true
        });

        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        ClientAdapter.Received().BuildErrorMessage("tts_auth_failed", "credentials rejected", Arg.Any<string>());
        FakeWssClient.DisconnectCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task NullErrorData_FallsBackToNonCriticalTtsSynthesisFailed()
    {
        // Pins the `errorData ?? new RealtimeAiErrorData { ... IsCritical = false }` fallback at
        // Event.cs:192-197. A provider that raises the event with no payload must not be treated
        // as fatal — defaulting the other way would drop calls on a null.
        var sessionTask = await StartSessionInBackgroundAsync(UseFailingTts());

        await _tts.SimulateSynthesisFailedAsync(null);
        await Task.Delay(50);

        ClientAdapter.Received().BuildErrorMessage("TtsSynthesisFailed", "TTS synthesis failed.", Arg.Any<string>());
        FakeWssClient.DisconnectCallCount.ShouldBe(0);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AudioModeSynthesisFailure_DoesNotCompleteTheTurn()
    {
        // In audio mode the turn gate does not wait on the TTS leg, so a synthesis failure must not
        // synthesise a turn completion. Guards the `if (UsesExternalTts)` gate at Event.cs:199-200:
        // dropping it would send a spurious turn-completed frame and arm the idle timer early.
        var sessionTask = await StartSessionInBackgroundAsync(UseFailingTts());

        await _tts.SimulateSynthesisFailedAsync(new RealtimeAiErrorData { Code = "x", Message = "y", IsCritical = false });
        await Task.Delay(50);

        ClientAdapter.DidNotReceive().BuildTurnCompletedMessage(Arg.Any<string>());
        TimerManager.DidNotReceive().StartTimer(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Func<Task>>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SessionTeardown_StopsTheTtsProvider()
    {
        var sessionTask = await StartSessionInBackgroundAsync(UseFailingTts());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        _tts.StopCount.ShouldBe(1, "leaving a TTS socket open after the call ends leaks it for the process lifetime");
    }
}
