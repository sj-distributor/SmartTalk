using System.Runtime.CompilerServices;
using Serilog;

namespace SmartTalk.UnitTests.Utils;

/// <summary>
/// Points the global <see cref="Log.Logger"/> at the TestCorrelator sink for the whole test
/// assembly, so tests can assert on what the production code logs.
///
/// <para>Production code throughout this codebase logs through the static <c>Serilog.Log</c>
/// rather than an injected <c>ILogger</c>. Left at its default, <c>Log.Logger</c> is a silent
/// logger that discards everything — which means a test asserting "this call must not log the
/// caller's phone number" would pass whether or not the code leaks it. Wiring a real sink is
/// what makes those assertions mean something.</para>
///
/// <para><c>[ModuleInitializer]</c> rather than an xunit fixture: the assignment must happen
/// exactly once before any test runs, and tying it to a collection fixture would force every
/// log-asserting test into one serialized collection.</para>
///
/// <para>Parallel safety comes from TestCorrelator itself — it tags events with an
/// <c>AsyncLocal</c> context id, so concurrently running test classes each read back only their
/// own events. A plain collecting sink would interleave them.</para>
///
/// <para>Guarded by <c>SerilogTestCaptureTests</c> (under Services/RealtimeAiV2/Infrastructure,
/// so the RealtimeAiV2 gate covers it). If those fail, every log assertion in the suite is
/// silently vacuous.</para>
/// </summary>
internal static class SerilogTestCapture
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Log.Logger = new LoggerConfiguration()
            // Verbose: some assertions target events the chain writes at Debug. An Information
            // floor would drop them and make those tests impossible to write.
            .MinimumLevel.Verbose()
            // Required by the correlation work — RealtimeSessionId / TraceId / CallSid all ride
            // on LogContext.PushProperty and never reach the sink without this.
            .Enrich.FromLogContext()
            .WriteTo.TestCorrelator()
            .CreateLogger();
    }
}
