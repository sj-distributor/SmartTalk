using Serilog;
using Serilog.Context;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Core.Logging;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Infrastructure;

/// <summary>
/// <see cref="DeferredLogScope"/> exists for one reason: some correlation keys are not known when
/// the scope has to open.
///
/// <para>A phone call reaches the consumer with a trace id but no CallSid — Twilio only sends that on
/// its <c>start</c> frame, several logged steps later. Serilog's own
/// <c>LogContext.PushProperty(name, value)</c> cannot express this: its enricher resolves the value
/// once and caches it, so pushing a placeholder and mutating it afterwards changes nothing. The
/// alternative — reopening a nested scope once CallSid arrives — leaves every earlier line of that
/// call uncorrelated and unreachable in Seq.</para>
/// </summary>
public class DeferredLogScopeTests
{
    [Fact]
    public void ValueSetAfterScopeOpens_ShouldAppearOnSubsequentEvents()
    {
        // The behaviour the whole type exists for.
        using var context = TestCorrelator.CreateContext();
        var scope = new DeferredLogScope().Set("TraceId", "trace-1");

        using (LogContext.Push(scope))
        {
            Log.Information("before");
            scope.Set("CallSid", "CA-late");
            Log.Information("after");
        }

        var events = TestCorrelator.GetLogEventsFromCurrentContext().ToList();

        events[0].Properties.ContainsKey("TraceId").ShouldBeTrue();
        events[0].Properties.ContainsKey("CallSid").ShouldBeFalse();
        events[1].Properties["CallSid"].ToString().ShouldBe("\"CA-late\"");
        events[1].Properties["TraceId"].ToString().ShouldBe("\"trace-1\"");
    }

    [Fact]
    public void ValuesShouldFlowIntoBackgroundTasksStartedInsideTheScope()
    {
        // This is how the provider WSS client's own log lines get correlated without touching any of
        // them: the receive loop is started with Task.Run inside the engine's scope, so it inherits
        // the ambient LogContext.
        using var context = TestCorrelator.CreateContext();
        var scope = new DeferredLogScope().Set("RealtimeSessionId", "session-1");

        using (LogContext.Push(scope))
        {
            Task.Run(() => Log.Information("from background")).Wait();
        }

        TestCorrelator.GetLogEventsFromCurrentContext()
            .ShouldHaveSingleItem()
            .Properties["RealtimeSessionId"].ToString().ShouldBe("\"session-1\"");
    }

    [Fact]
    public void DisposedScope_ShouldStopEnriching()
    {
        using var context = TestCorrelator.CreateContext();
        var scope = new DeferredLogScope().Set("TraceId", "trace-1");

        using (LogContext.Push(scope))
        {
            Log.Information("inside");
        }

        Log.Information("outside");

        var events = TestCorrelator.GetLogEventsFromCurrentContext().ToList();

        events[0].Properties.ContainsKey("TraceId").ShouldBeTrue();
        events[1].Properties.ContainsKey("TraceId").ShouldBeFalse();
    }

    [Fact]
    public void NullValue_ShouldRemoveThePropertyRatherThanLogNull()
    {
        // A "TraceId": null line is worse than no property: it pollutes the Seq facet with a value
        // nobody can filter on.
        using var context = TestCorrelator.CreateContext();
        var scope = new DeferredLogScope().Set("Optional", "present").Set("Optional", null);

        using (LogContext.Push(scope))
        {
            Log.Information("probe");
        }

        TestCorrelator.GetLogEventsFromCurrentContext()
            .ShouldHaveSingleItem()
            .Properties.ContainsKey("Optional").ShouldBeFalse();
    }

    [Fact]
    public void ExplicitEventProperty_ShouldWinOverTheScope()
    {
        // Call-scope values are ambient defaults. A call site that deliberately names the property
        // means something more specific by it, so it must not be silently overwritten.
        using var context = TestCorrelator.CreateContext();
        var scope = new DeferredLogScope().Set("CallSid", "from-scope");

        using (LogContext.Push(scope))
        {
            Log.Information("probe {CallSid}", "from-call-site");
        }

        TestCorrelator.GetLogEventsFromCurrentContext()
            .ShouldHaveSingleItem()
            .Properties["CallSid"].ToString().ShouldBe("\"from-call-site\"");
    }
}
