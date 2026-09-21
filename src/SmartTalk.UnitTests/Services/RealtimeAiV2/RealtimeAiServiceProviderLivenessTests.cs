using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// A half-open provider connection — a firewall dropping the state table with no FIN and no RST —
/// parks ReceiveAsync forever while the socket still reports Open. Nothing in the engine notices, and
/// on the built-in audio path (every phone call today) no turn watchdog arms either, so the caller
/// sits in silence on a live billed call until TCP retry exhausts roughly fifteen minutes later.
///
/// <para>The observer only records. Raising ConnectionLost on silence would be the obvious fix and
/// the dangerous one to guess at: it is classified critical, so it hangs up on the caller, and a
/// threshold set slightly too low would manufacture more dropped calls than the fault it targets.
/// These tests pin that it stays passive, and that it only measures while a response is supposedly
/// streaming — silence between turns is a caller thinking.</para>
/// </summary>
public class RealtimeAiServiceProviderLivenessTests : RealtimeAiServiceTestBase
{
    private void UseFastObserver()
    {
        Sut.ProviderSilenceThresholdOverride = TimeSpan.FromMilliseconds(120);
        Sut.ProviderLivenessPollIntervalOverride = TimeSpan.FromMilliseconds(20);
    }

    private void ProviderStartsAResponse() =>
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted });

    private static IEnumerable<LogEvent> SilenceObservations() =>
        TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(e => e.MessageTemplate.Text.Contains("Provider silent"));

    [Fact]
    public async Task ProviderGoesQuietMidResponse_ShouldRecordTheGap()
    {
        using var context = TestCorrelator.CreateContext();
        UseFastObserver();
        ProviderStartsAResponse();

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("response.created");
        await Task.Delay(400);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var observation = SilenceObservations().First();

        observation.Level.ShouldBe(LogEventLevel.Warning);
        double.Parse(observation.Properties["GapMs"].ToString()).ShouldBeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public async Task ProviderQuietBetweenTurns_ShouldRecordNothing()
    {
        // The reverse case, and the reason the observer is gated: a caller thinking is not a fault,
        // and reporting it would bury the signal that matters.
        using var context = TestCorrelator.CreateContext();
        UseFastObserver();

        var sessionTask = await StartSessionInBackgroundAsync();
        await Task.Delay(400);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        SilenceObservations().ShouldBeEmpty();
    }

    [Fact]
    public async Task ObservedSilence_ShouldNotEndTheSessionOrNotifyTheClient()
    {
        // The whole point of this phase: measure, do not act. Acting on a guessed threshold would
        // hang up on callers.
        using var context = TestCorrelator.CreateContext();
        UseFastObserver();
        ProviderStartsAResponse();

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("response.created");
        await Task.Delay(400);

        SilenceObservations().ShouldNotBeEmpty("the observation must have fired for this test to mean anything");
        sessionTask.IsCompleted.ShouldBeFalse();
        FakeWssClient.DisconnectCallCount.ShouldBe(0);
        ClientAdapter.DidNotReceive().BuildErrorMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ContinuedSilence_ShouldBeRecordedOncePerGapNotOncePerPoll()
    {
        // A wedged connection would otherwise emit a line every poll for the rest of the call.
        using var context = TestCorrelator.CreateContext();
        UseFastObserver();
        ProviderStartsAResponse();

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("response.created");
        await Task.Delay(600);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        SilenceObservations().Count().ShouldBe(1);
    }
}
