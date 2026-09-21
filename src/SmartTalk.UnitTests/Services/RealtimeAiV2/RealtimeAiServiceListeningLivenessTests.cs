using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Liveness;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The engine's largest blind spot was the window where it is LISTENING. The silence observer arms
/// only while a response is in flight; the turn hard ceiling arms from the same event; and the idle
/// follow-up timer is STOPPED the moment the provider reports the caller started speaking, restarting
/// only when a turn completes. In the state "provider said speech_started, engine is waiting for the
/// response" all three are off, and the production phone path sets no session ceiling behind them.
///
/// <para>A provider socket half-opening there is invisible: nothing observes it, nothing bounds it,
/// nothing logs it. The caller talks into a dead line until they hang up, and the call then reports
/// the same Outcome as one a satisfied caller ended.</para>
///
/// <para>Still record-only, matching the existing observer's deliberate passivity — acting on a
/// guessed threshold would manufacture dropped calls. What these pin beyond that is the clear-point:
/// it is read off the parsed EVENT TYPE, not off a provider-specific start signal, because the Google
/// adapter never emits ResponseStarted at all. Keyed on that, a Google session would stay
/// "awaiting" forever after its first barge-in and report every ordinary caller pause as a fault.</para>
/// </summary>
public class RealtimeAiServiceListeningLivenessTests : RealtimeAiServiceTestBase
{
    private void UseFastObserver()
    {
        Sut.ProviderSilenceThresholdOverride = TimeSpan.FromMilliseconds(120);
        Sut.ListeningSilenceThresholdOverride = TimeSpan.FromMilliseconds(120);
        Sut.ProviderLivenessPollIntervalOverride = TimeSpan.FromMilliseconds(20);
    }

    private void ProviderEmits(RealtimeAiWssEventType type, object data = null) =>
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent { Type = type, Data = data });

    private static IEnumerable<LogEvent> ListeningObservations() =>
        TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(e => e.MessageTemplate.Text.Contains("No provider traffic while awaiting a response"));

    [Fact]
    public async Task ProviderSilentAfterReportingTheCallerSpoke_ShouldRecordTheGap()
    {
        using var context = TestCorrelator.CreateContext();
        UseFastObserver();
        ProviderEmits(RealtimeAiWssEventType.SpeechDetected);

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("speech_started");
        await Task.Delay(400);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var observation = ListeningObservations().FirstOrDefault();

        observation.ShouldNotBeNull("a caller talking into a dead line is the case this exists for");
        observation.Level.ShouldBe(LogEventLevel.Warning);
        double.Parse(observation.Properties["GapMs"].ToString()).ShouldBeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public async Task AProviderThatAnswersWithAudioAndNeverAnnouncesAResponse_ShouldRecordNothing()
    {
        // The Google shape. Its adapter maps serverContent to audio deltas and turn-complete and never
        // produces ResponseStarted, so the window has to close on the audio itself. Keyed on a start
        // signal instead, every pause in a healthy Google call would be reported as provider silence.
        using var context = TestCorrelator.CreateContext();
        UseFastObserver();

        var sessionTask = await StartSessionInBackgroundAsync();

        ProviderEmits(RealtimeAiWssEventType.SpeechDetected);
        await FakeWssClient.SimulateMessageReceivedAsync("interrupted");

        ProviderEmits(RealtimeAiWssEventType.ResponseAudioDelta, new RealtimeAiWssAudioData { Base64Payload = Convert.ToBase64String(new byte[320]) });
        await FakeWssClient.SimulateMessageReceivedAsync("audio");

        await Task.Delay(400);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        ListeningObservations().ShouldBeEmpty("the provider answered — there is no dead line to report");
    }

    [Fact]
    public async Task ASessionWhereTheCallerNeverSpoke_ShouldRecordNothing()
    {
        // The window opens on the provider reporting speech, not on connect. Silence before that is a
        // caller who has not said anything yet, which is not a fault.
        using var context = TestCorrelator.CreateContext();
        UseFastObserver();

        var sessionTask = await StartSessionInBackgroundAsync();
        await Task.Delay(400);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        ListeningObservations().ShouldBeEmpty();
    }

    [Fact]
    public async Task AnObservedListeningGap_ShouldNotEndTheSessionOrNotifyTheClient()
    {
        // Same stance as the in-response observer: measure, do not act. A threshold guessed wrong must
        // cost a log line, never a caller's call.
        using var context = TestCorrelator.CreateContext();
        UseFastObserver();
        ProviderEmits(RealtimeAiWssEventType.SpeechDetected);

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("speech_started");
        await Task.Delay(400);

        ListeningObservations().ShouldNotBeEmpty("the test is vacuous unless the observation actually fired");
        FakeWssClient.DisconnectCallCount.ShouldBe(0);
        ClientAdapter.DidNotReceive().BuildErrorMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ListeningThreshold_ShouldStayWellAboveTheInResponseOne()
    {
        // A caller reading an order aloud produces transcription frames, which keep the gap timer
        // reset, so this only grows when the provider sends literally nothing. It is still set far
        // above the in-response threshold: no legitimate call has the provider go completely silent
        // for this long, and the cost of being wrong is a Warning on a healthy call.
        RealtimeAiLivenessDefaults.InResponse.ShouldBe(TimeSpan.FromSeconds(20));
        RealtimeAiLivenessDefaults.WhileListening.ShouldBe(TimeSpan.FromSeconds(45));
    }
}
