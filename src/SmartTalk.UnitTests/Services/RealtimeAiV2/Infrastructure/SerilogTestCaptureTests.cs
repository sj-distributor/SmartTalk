using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Infrastructure;

/// <summary>
/// Self-test for the log-capture seam the RealtimeAiV2 logging work is built on.
///
/// <para>The hardening plan's observation phase changes what the chain logs — correlation
/// properties, latency values, and the removal of PII. None of that is assertable without a
/// sink the tests can read, so this fixture is a hard prerequisite: if these tests fail, every
/// log-related test downstream is silently vacuous rather than failing loudly.</para>
///
/// <para><see cref="TestCorrelator"/> scopes captured events to an <c>AsyncLocal</c> context, so
/// test classes running in parallel each see only their own events. Swapping
/// <see cref="Log.Logger"/> for a plain collecting sink would cross-contaminate them.</para>
/// </summary>
public class SerilogTestCaptureTests
{
    [Fact]
    public void GlobalLogger_ShouldRouteEventsToTestCorrelator()
    {
        using var context = TestCorrelator.CreateContext();

        Log.Information("probe {Marker}", "capture-seam");

        TestCorrelator.GetLogEventsFromCurrentContext()
            .ShouldHaveSingleItem()
            .MessageTemplate.Text.ShouldBe("probe {Marker}");
    }

    [Fact]
    public void GlobalLogger_ShouldEnrichFromLogContext()
    {
        // The correlation work (RealtimeSessionId / TraceId / CallSid) rides entirely on
        // LogContext.PushProperty. Without Enrich.FromLogContext() on the test logger those
        // properties never reach the sink and the correlation tests would pass vacuously.
        using var context = TestCorrelator.CreateContext();

        using (LogContext.PushProperty("RealtimeSessionId", "session-abc"))
        {
            Log.Information("probe");
        }

        TestCorrelator.GetLogEventsFromCurrentContext()
            .ShouldHaveSingleItem()
            .Properties["RealtimeSessionId"].ToString().ShouldBe("\"session-abc\"");
    }

    [Fact]
    public void GlobalLogger_ShouldCaptureVerboseEvents()
    {
        // Several planned assertions target events the chain writes at Debug (e.g. the
        // known-benign provider events that stop being Warnings). A default Information
        // minimum would drop them and make those tests unwritable.
        using var context = TestCorrelator.CreateContext();

        Log.Verbose("verbose probe");
        Log.Debug("debug probe");

        TestCorrelator.GetLogEventsFromCurrentContext()
            .Select(e => e.Level)
            .ShouldBe(new[] { LogEventLevel.Verbose, LogEventLevel.Debug });
    }
}
