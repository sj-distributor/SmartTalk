using System.Net.WebSockets;
using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// A session used to end with two Warnings that blamed the caller — "WebSocket cancelled" and
/// "Client disconnected abnormally" — regardless of whether the caller hung up, the configured
/// ceiling fired, or the engine tore the session down itself after a provider fault.
///
/// <para>Two consequences: an on-call engineer reading the first line of a dropped-call complaint is
/// pointed at the telephony vendor for a failure the server chose, and "abnormal disconnect rate" is
/// permanently non-zero, so it cannot be alerted on. One outcome property, low-cardinality enough to
/// facet, replaces both.</para>
///
/// <para>The outcome is also what P3.5 needs: a consumer cannot decide whether to fall back to a
/// human until it can tell a provider fault from a hangup.</para>
/// </summary>
public class RealtimeAiServiceSessionOutcomeTests : RealtimeAiServiceTestBase
{
    private static LogEvent SessionEndedLine()
    {
        var captured = TestCorrelator.GetLogEventsFromCurrentContext().ToList();
        var ended = captured.Where(e => e.MessageTemplate.Text.Contains("Session ended")).ToList();

        // Says what it saw instead of just throwing on Single(): when this fails under a loaded run
        // the useful question is whether cleanup ran at all, and that is not visible otherwise.
        ended.Count.ShouldBe(1,
            $"expected one Session ended line, saw {ended.Count} among {captured.Count} events: " +
            string.Join(" | ", captured.Select(e => e.MessageTemplate.Text)));

        return ended[0];
    }

    private static string Outcome(LogEvent line) => line.Properties["Outcome"].ToString().Trim('"');

    [Fact]
    public async Task CallerHangsUp_ShouldReportClientClosedAtInformation()
    {
        using var context = TestCorrelator.CreateContext();

        var sessionTask = await StartSessionInBackgroundAsync();
        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var line = SessionEndedLine();

        Outcome(line).ShouldBe(nameof(RealtimeAiSessionOutcome.ClientClosed));
        line.Level.ShouldBe(LogEventLevel.Information, "a caller hanging up is the normal ending, not a warning");
    }

    [Fact]
    public async Task ProviderFault_ShouldReportProviderFaultAtErrorAndNotBlameTheClient()
    {
        using var context = TestCorrelator.CreateContext();

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateStateChangedAsync(WebSocketState.Closed, "server closed");
        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var line = SessionEndedLine();

        Outcome(line).ShouldBe(nameof(RealtimeAiSessionOutcome.ProviderFault));
        line.Level.ShouldBe(LogEventLevel.Error);

        TestCorrelator.GetLogEventsFromCurrentContext()
            .ShouldNotContain(e => e.MessageTemplate.Text.Contains("Client disconnected abnormally"),
                "the server chose this teardown; blaming the caller sends incident response to the wrong vendor");
    }

    [Fact]
    public async Task DurationCeilingFires_ShouldReportMaxDurationReachedAtInformation()
    {
        using var context = TestCorrelator.CreateContext();

        // The ceiling races the harness: it is armed when the context is built, while the session is
        // still coming up. 600ms was not enough headroom under a loaded full-suite run — the ceiling
        // fired before the session was up and teardown took a different path. Sized for several times
        // the observed startup cost rather than trimmed to keep the test fast.
        var options = CreateDefaultOptions(o => o.MaxSessionDuration = TimeSpan.FromSeconds(2));
        var sessionTask = await StartSessionInBackgroundAsync(options);
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var line = SessionEndedLine();

        Outcome(line).ShouldBe(nameof(RealtimeAiSessionOutcome.MaxDurationReached));
        line.Level.ShouldBe(LogEventLevel.Information, "reaching a configured ceiling is by design");
    }

    [Fact]
    public async Task ClientTransportFails_ShouldReportClientAbortedAtWarning()
    {
        using var context = TestCorrelator.CreateContext();

        var sessionTask = await StartSessionInBackgroundAsync();
        FakeWs.EnqueueError(new WebSocketException("simulated transport failure"));
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var line = SessionEndedLine();

        Outcome(line).ShouldBe(nameof(RealtimeAiSessionOutcome.ClientAborted));
        line.Level.ShouldBe(LogEventLevel.Warning);
    }

    [Fact]
    public async Task SessionEndedLine_ShouldCarryTheCountersAnOnCallEngineerAsksFor()
    {
        using var context = TestCorrelator.CreateContext();

        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.InputAudioTranscriptionCompleted,
            Data = new RealtimeAiWssTranscriptionData { Transcript = "hi", Speaker = SmartTalk.Messages.Enums.AiSpeechAssistant.AiSpeechAssistantSpeaker.User }
        });

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("t");
        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var line = SessionEndedLine();

        line.Properties["TurnCount"].ToString().ShouldBe("0");
        line.Properties["TranscriptionCount"].ToString().ShouldBe("1");
        double.Parse(line.Properties["ElapsedSessionMs"].ToString()).ShouldBeGreaterThanOrEqualTo(0);
    }
}
