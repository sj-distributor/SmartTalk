using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace SmartTalk.Core.Logging;

/// <summary>
/// An ambient log scope whose property values may be supplied after the scope is opened.
///
/// <para>Serilog's own <c>LogContext.PushProperty(name, value)</c> resolves and caches its value the
/// first time it enriches an event, so a placeholder pushed early cannot be filled in later. That is
/// a problem whenever the identifier a scope exists to carry arrives mid-flow: a phone call reaches
/// the consumer with a trace id but no CallSid, because Twilio only sends that on its <c>start</c>
/// frame — several logged steps in. Opening a nested scope at that point would leave every earlier
/// line of the call uncorrelated.</para>
///
/// <para>Push once at the outermost frame, <see cref="Set"/> values as they become known:</para>
/// <code>
/// var scope = new DeferredLogScope().Set("TraceId", traceId);
/// using (LogContext.Push(scope))
/// {
///     // ... later, once the transport hands it over:
///     scope.Set("CallSid", callSid);
/// }
/// </code>
///
/// <para>Values reach code the scope calls into, including work started with <c>Task.Run</c> inside
/// it, because <c>LogContext</c> flows on the execution context. That is what correlates log lines
/// written by transport clients without editing any of their call sites.</para>
///
/// <para>A property named explicitly at a call site always wins: scope values are ambient defaults,
/// and a call site that names the property means something more specific by it.</para>
/// </summary>
public sealed class DeferredLogScope : ILogEventEnricher
{
    private readonly ConcurrentDictionary<string, object> _values = new();

    /// <summary>Sets a value, or removes the property when <paramref name="value"/> is null.</summary>
    public DeferredLogScope Set(string name, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // A property logged as null pollutes the Seq facet with a value nobody can filter on;
        // absent is strictly more useful than present-and-empty.
        if (value is null)
            _values.TryRemove(name, out _);
        else
            _values[name] = value;

        return this;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var (name, value) in _values)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(name, value));
        }
    }
}
