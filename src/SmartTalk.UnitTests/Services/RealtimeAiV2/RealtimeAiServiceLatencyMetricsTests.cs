using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// There was not a single duration on any log line in 5600 lines of engine.
///
/// <para>That makes the most common complaint — "there's a long pause before the AI answers" —
/// unanswerable: you can subtract timestamps by hand for one call, but you cannot compute a p95, and
/// you cannot tell whether a regression sits in the prompt fan-out, the TTS handshake, the provider
/// handshake, or generation itself. It also leaves the hardening plan's later thresholds
/// (a turn ceiling, a function-call timeout, a provider-silence bound) to be guessed rather than
/// derived from what production actually does.</para>
///
/// <para>Measured with <c>Stopwatch.GetTimestamp()</c> deltas: monotonic, unaffected by clock
/// adjustments, and allocation-free.</para>
/// </summary>
public class RealtimeAiServiceLatencyMetricsTests : RealtimeAiServiceTestBase
{
    private static LogEvent LineContaining(string fragment) =>
        TestCorrelator.GetLogEventsFromCurrentContext().Single(e => e.MessageTemplate.Text.Contains(fragment));

    private static double NumericProperty(LogEvent logEvent, string name) =>
        double.Parse(logEvent.Properties[name].ToString());

    [Fact]
    public async Task ProviderConnectedLine_ShouldReportConnectLatencyBrokenDown()
    {
        using var context = TestCorrelator.CreateContext();

        var sessionTask = await StartSessionInBackgroundAsync();
        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var connected = LineContaining("Connected to provider");

        // Split so a slow connect points at a layer instead of just being slow.
        NumericProperty(connected, "ElapsedConnectMs").ShouldBeGreaterThanOrEqualTo(0);
        NumericProperty(connected, "ElapsedTtsInitMs").ShouldBeGreaterThanOrEqualTo(0);
        NumericProperty(connected, "ElapsedProviderHandshakeMs").ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ProviderReadyLine_ShouldReportTimeUntilTheProviderConfirmedTheSession()
    {
        using var context = TestCorrelator.CreateContext();

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SessionInitialized });

        var sessionTask = await StartSessionInBackgroundAsync();
        await Task.Delay(60);
        await FakeWssClient.SimulateMessageReceivedAsync("session.updated");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        // "Connected" today means the socket opened, not that the provider accepted the session.
        NumericProperty(LineContaining("Provider session initialized"), "ElapsedProviderReadyMs")
            .ShouldBeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public async Task FirstAudioOfATurn_ShouldReportTheCallerAudiblePause()
    {
        // The number that maps directly onto what the caller experiences as a pause.
        using var context = TestCorrelator.CreateContext();

        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0) switch
        {
            "started" => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted },
            _ => new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseAudioDelta,
                Data = new RealtimeAiWssAudioData { Base64Payload = Convert.ToBase64String(new byte[160]) }
            }
        });

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await Task.Delay(60);
        await FakeWssClient.SimulateMessageReceivedAsync("audio");
        await FakeWssClient.SimulateMessageReceivedAsync("audio");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Exactly one line per turn — the second delta must not emit another.
        NumericProperty(LineContaining("First audio of turn"), "ElapsedToFirstAudioMs")
            .ShouldBeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public async Task TurnCompletedLine_ShouldReportTurnDuration()
    {
        using var context = TestCorrelator.CreateContext();

        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0) switch
        {
            "started" => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted },
            _ => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseTurnCompleted }
        });

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await Task.Delay(60);
        await FakeWssClient.SimulateMessageReceivedAsync("done");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Feeds the hard-ceiling threshold P3.1 needs: without the real p99.9 it would be a guess.
        NumericProperty(LineContaining("AI turn completed"), "ElapsedTurnMs").ShouldBeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public async Task FunctionCall_ShouldReportHandlerDuration()
    {
        using var context = TestCorrelator.CreateContext();

        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.FunctionCallSuggested,
            Data = new List<RealtimeAiWssFunctionCallData> { new() { FunctionName = "repeat_order", CallId = "c1" } }
        });

        var options = CreateDefaultOptions(o => o.OnFunctionCallAsync = async (_, _) =>
        {
            await Task.Delay(60);
            return new RealtimeAiFunctionCallResult { Output = "ok" };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("fc");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Feeds the per-call timeout P3.4a needs, and shows which tool is blocking the receive loop.
        var completed = LineContaining("Function call completed");

        NumericProperty(completed, "ElapsedFunctionCallMs").ShouldBeGreaterThanOrEqualTo(50);
        completed.Properties["FunctionName"].ToString().ShouldContain("repeat_order");
    }
}
