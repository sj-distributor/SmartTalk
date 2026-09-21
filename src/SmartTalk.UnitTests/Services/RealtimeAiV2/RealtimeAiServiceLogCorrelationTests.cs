using Serilog.Sinks.TestCorrelator;
using Shouldly;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The engine's half of the call-correlation contract.
///
/// <para>Before this, engine log lines carried <c>SessionId</c> only where a call site happened to
/// pass it as a template argument, and the provider WSS client's own ~24 lines carried no call
/// identity at all — so a live incident could not be filtered down to one phone call in Seq.</para>
///
/// <para>Pushing the id once as an ambient property fixes both: every line inside the session, and
/// every line from code the session calls, is tagged without editing those call sites.</para>
/// </summary>
public class RealtimeAiServiceLogCorrelationTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task EveryEngineLogDuringTheSession_ShouldCarryRealtimeSessionId()
    {
        using var context = TestCorrelator.CreateContext();

        var sessionTask = await StartSessionInBackgroundAsync();
        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var engineEvents = TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(e => e.MessageTemplate.Text.StartsWith("[RealtimeAi]"))
            .ToList();

        engineEvents.ShouldNotBeEmpty("the session must have logged something for this test to mean anything");
        engineEvents.ShouldAllBe(e => e.Properties.ContainsKey("RealtimeSessionId"));
    }

    [Fact]
    public async Task ConsumerSuppliedSessionId_ShouldBeUsedInsteadOfAFreshGuid()
    {
        // Lets the consumer mint the id before ConnectAsync, so its own earlier log lines — the ones
        // written while resolving the agent and building the prompt — can carry the same value.
        using var context = TestCorrelator.CreateContext();
        const string consumerSessionId = "consumer-owned-session-id";

        var options = CreateDefaultOptions(o => o.SessionId = consumerSessionId);
        var sessionTask = await StartSessionInBackgroundAsync(options);
        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var correlated = TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(e => e.Properties.ContainsKey("RealtimeSessionId"))
            .ToList();

        // ShouldAllBe is vacuously true on an empty sequence — without this the whole test would
        // pass while the property was never emitted at all.
        correlated.ShouldNotBeEmpty();
        correlated.ShouldAllBe(e => e.Properties["RealtimeSessionId"].ToString() == "\"" + consumerSessionId + "\"");
    }

    [Fact]
    public async Task WithoutAConsumerSessionId_TheEngineStillGeneratesOne()
    {
        using var context = TestCorrelator.CreateContext();

        var sessionTask = await StartSessionInBackgroundAsync(CreateDefaultOptions());
        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var correlated = TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(e => e.Properties.ContainsKey("RealtimeSessionId"))
            .ToList();

        correlated.ShouldNotBeEmpty();
        correlated.ShouldAllBe(e => e.Properties["RealtimeSessionId"].ToString().Length > 2);
    }

    [Fact]
    public async Task SuccessiveSessions_ShouldNotShareCorrelationIds()
    {
        // The scope must be per-session, not per-service. A static or otherwise shared holder would
        // make Seq attribute one caller's lines to another call.
        using var context = TestCorrelator.CreateContext();

        var first = await StartSessionInBackgroundAsync(CreateDefaultOptions(o => o.SessionId = "session-one"));
        FakeWs.EnqueueClose();
        await first.WaitAsync(TimeSpan.FromSeconds(5));

        var second = await StartSessionInBackgroundAsync(CreateDefaultOptions(o => o.SessionId = "session-two"));
        FakeWs.EnqueueClose();
        await second.WaitAsync(TimeSpan.FromSeconds(5));

        var ids = TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(e => e.Properties.ContainsKey("RealtimeSessionId"))
            .Select(e => e.Properties["RealtimeSessionId"].ToString())
            .Distinct()
            .ToList();

        ids.ShouldBe(new[] { "\"session-one\"", "\"session-two\"" }, ignoreOrder: true);
    }
}
