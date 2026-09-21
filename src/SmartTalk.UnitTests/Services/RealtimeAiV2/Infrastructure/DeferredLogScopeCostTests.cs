using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Shouldly;
using SmartTalk.Core.Logging;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Infrastructure;

/// <summary>
/// Pins the cost characteristics an always-on ambient scope has to have, because it sits on the path
/// of every log line the realtime chain writes.
///
/// <para>The load-bearing one is <see cref="FilteredOutEvents_ShouldNotEnrich"/>. The provider WSS
/// clients log every inbound message at Debug — including audio deltas, the highest-rate path in the
/// system (OpenAiRealtimeAiWssClient.cs:88). Production runs at Serilog's default Information
/// minimum, so those never materialise; but that only holds if enrichment happens after the level
/// check, never before. If it were the other way round, raising the log level for one investigation
/// would put per-audio-frame enrichment on the hot path.</para>
/// </summary>
public class DeferredLogScopeCostTests
{
    private sealed class CountingEnricher : ILogEventEnricher
    {
        public int Invocations;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) => Invocations++;
    }

    [Fact]
    public void FilteredOutEvents_ShouldNotEnrich()
    {
        var counter = new CountingEnricher();

        // A private logger, not the assembly-wide test one, so the minimum level is the thing under
        // test rather than the capture configuration.
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .CreateLogger();

        using (LogContext.Push(counter))
        {
            logger.Debug("below the floor");
            logger.Verbose("further below the floor");

            counter.Invocations.ShouldBe(0,
                "enrichment must happen after the level check — otherwise every per-message Debug in the WSS clients would pay for the scope");

            logger.Information("at the floor");

            counter.Invocations.ShouldBe(1);
        }
    }

    [Fact]
    public void RepeatedEnrichment_ShouldNotGrowTheScope()
    {
        // The scope is held for the lifetime of a call. Anything that accumulated per event would be
        // an unbounded leak on a long call.
        var scope = new DeferredLogScope().Set(LogProperties.RealtimeSessionId, "session-1");
        var factory = new ProbeFactory();

        for (var i = 0; i < 1000; i++)
        {
            scope.Enrich(new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
                new MessageTemplate("probe", []), []), factory);
        }

        factory.PropertiesCreated.ShouldBe(1000, "one property per event per key — no accumulation across events");
    }

    [Fact]
    public void SettingTheSameKeyRepeatedly_ShouldOverwriteNotAccumulate()
    {
        // Back-filling is a Set on an existing key (CallSid arrives, then AgentId, then a corrected
        // AgentId). Accumulating would make the scope grow for the whole call.
        var scope = new DeferredLogScope();

        for (var i = 0; i < 1000; i++)
        {
            scope.Set(LogProperties.CallSid, $"CA-{i}");
        }

        var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            new MessageTemplate("probe", []), []);

        scope.Enrich(logEvent, new ProbeFactory());

        logEvent.Properties.Count.ShouldBe(1);
        logEvent.Properties[LogProperties.CallSid].ToString().ShouldBe("\"CA-999\"");
    }

    [Fact]
    public void EnrichingConcurrently_ShouldNotThrowWhileValuesAreBeingSet()
    {
        // Real shape: the Twilio start frame sets CallSid on the consumer's thread while the engine's
        // receive loop is already writing log lines. A non-concurrent dictionary would throw here.
        var scope = new DeferredLogScope().Set(LogProperties.RealtimeSessionId, "session-1");
        var factory = new ProbeFactory();

        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 2000; i++) scope.Set(LogProperties.CallSid, $"CA-{i}");
        });

        var enricher = Task.Run(() =>
        {
            for (var i = 0; i < 2000; i++)
            {
                scope.Enrich(new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
                    new MessageTemplate("probe", []), []), factory);
            }
        });

        Should.NotThrow(() => Task.WaitAll([writer, enricher], TimeSpan.FromSeconds(10)));
    }

    private sealed class ProbeFactory : ILogEventPropertyFactory
    {
        public int PropertiesCreated;

        public LogEventProperty CreateProperty(string name, object value, bool destructureObjects = false)
        {
            PropertiesCreated++;
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }
}
