using System.Collections.Concurrent;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;
using Xunit.Abstractions;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Reproduction harness for the intermittent failure in
/// <c>RealtimeAiServiceSessionOutcomeTests.DurationCeilingFires_ShouldReportMaxDurationReachedAtInformation</c>.
///
/// <para>That test fails rarely, and only inside the full suite — never on its own. It has been
/// widened twice already (200ms → 600ms → 2s) on the theory that the ceiling was simply firing
/// before the session was up. Widening a tolerance is not a diagnosis, and the same reflex was wrong
/// once before on this codebase: the provider-drop golden was read as flaky twice and turned out to
/// be a real teardown race (see <c>RealtimeAiServiceProviderDropStressTests</c>).</para>
///
/// <para>So this runs the same scenario many times concurrently, with a ceiling short enough to land
/// in the middle of startup, and reports WHICH of the three assertions diverged — how many
/// <c>Session ended</c> lines appeared, what outcome they carried, at what level — instead of just
/// going red. Nothing here changes production behaviour or the test it diagnoses.</para>
///
/// <para><b>What it found.</b> Not a test problem — a real misreport. The ceiling was applied as
/// <c>SessionCts.CancelAfter(ceiling)</c> and then inferred back at teardown by comparing
/// <c>Stopwatch.GetElapsedTime(SessionStartedAt)</c> against it. Those are two different clocks: the
/// cancellation runs off the timer queue, which can fire a tick before Stopwatch agrees the ceiling
/// has elapsed, and the comparison then falls through to ClientAborted. 25 of 600 iterations — 4% —
/// reported a session the server itself ended as the caller hanging up. The rate scales with the
/// ratio of that tick to the ceiling, which is why widening the tolerance kept appearing to help.</para>
///
/// <para>Fixed by recording the cause when the ceiling fires instead of inferring it afterwards
/// (RealtimeAiService.Context.cs, ApplyMaxSessionDurationIfRequired). Kept as the regression guard:
/// at the default iteration count it goes red on every run if that is reverted, so it needs no env
/// knob to be useful. <c>REALTIME_FLAKE_ITERATIONS=3000</c> for a longer soak.</para>
/// </summary>
[Trait("Category", "Stress")]
public class RealtimeAiServiceDurationCeilingStressTests
{
    private readonly ITestOutputHelper _output;

    public RealtimeAiServiceDurationCeilingStressTests(ITestOutputHelper output) => _output = output;

    private static int Iterations =>
        int.TryParse(Environment.GetEnvironmentVariable("REALTIME_FLAKE_ITERATIONS"), out var n) && n > 0 ? n : 120;

    [Fact]
    public async Task CeilingFiringDuringStartup_ShouldStillReportMaxDurationReached()
    {
        var failures = new ConcurrentBag<string>();
        var iterations = Iterations;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, iterations),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 },
            async (i, _) =>
            {
                using var scenario = new DurationCeilingScenario();

                var failure = await scenario.RunAsync();

                if (failure != null) failures.Add($"iteration {i}: {failure}");
            });

        foreach (var failure in failures.Take(10)) _output.WriteLine(failure);

        failures.ShouldBeEmpty($"{failures.Count}/{iterations} iterations diverged — see output for the first ten");
    }

    private sealed class DurationCeilingScenario : RealtimeAiServiceTestBase
    {
        // Deliberately inside the startup window rather than clear of it: the flake only shows when
        // the ceiling and the session coming up overlap, so the harness aims straight at that.
        private static readonly TimeSpan Ceiling = TimeSpan.FromMilliseconds(120);

        public async Task<string> RunAsync()
        {
            using var context = TestCorrelator.CreateContext();

            var options = CreateDefaultOptions(o => o.MaxSessionDuration = Ceiling);
            var sessionTask = await StartSessionInBackgroundAsync(options);

            try
            {
                await sessionTask.WaitAsync(TimeSpan.FromSeconds(20));
            }
            catch (TimeoutException)
            {
                return "session did not finish within 20s of a 120ms ceiling";
            }

            var captured = TestCorrelator.GetLogEventsFromCurrentContext().ToList();
            var ended = captured.Where(e => e.MessageTemplate.Text.Contains("Session ended")).ToList();

            if (ended.Count != 1)
                return $"expected one Session ended line, saw {ended.Count} among {captured.Count} events: " +
                       string.Join(" | ", captured.Select(e => e.MessageTemplate.Text));

            var outcome = ended[0].Properties.TryGetValue("Outcome", out var value) ? value.ToString().Trim('"') : "<no Outcome property>";

            if (outcome != nameof(RealtimeAiSessionOutcome.MaxDurationReached))
                return $"outcome was {outcome}, expected MaxDurationReached";

            if (ended[0].Level != LogEventLevel.Information)
                return $"level was {ended[0].Level}, expected Information";

            return null;
        }
    }
}
