using System.Collections.Concurrent;
using System.Net.WebSockets;
using NSubstitute;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Reproduction harness for the intermittent failure in
/// <c>RealtimeAiServiceNoReconnectBaselineGoldenTests.ProviderClosedWhileActive_CriticalConnectionLost_NoAutoReconnect</c>.
///
/// <para>That test fails roughly once in a few dozen runs, but only as part of the full suite —
/// never in isolation, which points at scheduling pressure rather than at the scenario. A flake in a
/// golden is worse than a missing test: the entire non-breaking gate rests on "goldens unchanged and
/// green", and an intermittent red trains everyone to ignore it.</para>
///
/// <para>This harness runs the same sequence many times concurrently so the failure can be observed
/// on demand, and — crucially — reports WHICH assertion broke instead of just going red. Nothing
/// here touches the golden, so the diagnosis can happen before deciding whether the golden needs to
/// change at all.</para>
///
/// <para><b>What it found.</b> Not a test problem — a real race in the engine.
/// <c>DisconnectFromProviderAsync</c> is a check-then-act on <c>_ctx.SessionCts</c>: it returns early
/// when the field is null, and later disposes it and sets it to null
/// (RealtimeAiService.Connect.cs:50-85). Two paths reach it concurrently when a provider drop ends a
/// live session — the critical-error path from <c>OnWssStateChangedAsync</c>, and the cleanup path as
/// the orchestration loop unwinds. Both pass the null check; the first disposes and nulls the field;
/// the second throws NullReferenceException at the Dispose call.</para>
///
/// <para>In production that exception is swallowed by the WSS client's receive-loop catch, leaving
/// the teardown half-finished — the TTS provider may not be stopped and the provider socket may not
/// be closed. The golden test was correctly detecting this all along; it was read as a flaky test
/// twice before this harness made the failure observable on demand.</para>
///
/// <para>At the default iteration count the race does not surface, so this stays green in the normal
/// gate. Reproduce with: <c>REALTIME_FLAKE_ITERATIONS=3000 dotnet test --filter ProviderDropStress</c>
/// — roughly one iteration in a few thousand. Remove the knob once the race is fixed and let the
/// default count carry the regression guard.</para>
/// </summary>
[Trait("Category", "Stress")]
public class RealtimeAiServiceProviderDropStressTests
{
    private readonly ITestOutputHelper _output;

    public RealtimeAiServiceProviderDropStressTests(ITestOutputHelper output) => _output = output;

    private static int Iterations =>
        int.TryParse(Environment.GetEnvironmentVariable("REALTIME_FLAKE_ITERATIONS"), out var n) && n > 0 ? n : 120;

    [Fact]
    public async Task ProviderDropWhileActive_ShouldBehaveIdenticallyUnderConcurrentLoad()
    {
        var failures = new ConcurrentBag<string>();
        var iterations = Iterations;

        // Concurrency is the reproduction condition: the scenario is deterministic on an idle
        // machine and only misbehaves when the thread pool is contended, which is exactly what the
        // full suite does to it.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, iterations),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 },
            async (i, _) =>
            {
                using var scenario = new ProviderDropScenario();

                var failure = await scenario.RunAsync();

                if (failure != null) failures.Add($"iteration {i}: {failure}");
            });

        foreach (var failure in failures.Take(10)) _output.WriteLine(failure);

        failures.ShouldBeEmpty($"{failures.Count}/{iterations} iterations diverged — see output for the first ten");
    }

    /// <summary>
    /// One self-contained run of the golden's sequence. Reuses the shared harness for setup, and
    /// returns a description of the first divergence rather than throwing, so a stress run can
    /// report how often and in which way it breaks instead of stopping at the first failure.
    /// </summary>
    private sealed class ProviderDropScenario : RealtimeAiServiceTestBase
    {
        public async Task<string?> RunAsync()
        {
            string? endedSessionId = null;
            var options = CreateDefaultOptions(o => o.OnSessionEndedAsync = id => { endedSessionId = id; return Task.CompletedTask; });

            var sessionTask = await StartSessionInBackgroundAsync(options);

            if (FakeWssClient.ConnectCallCount != 1)
                return $"ConnectCallCount was {FakeWssClient.ConnectCallCount} before the drop, expected 1";

            await FakeWssClient.SimulateStateChangedAsync(WebSocketState.Closed, "server closed");

            FakeWs.EnqueueClose();

            try
            {
                await sessionTask.WaitAsync(TimeSpan.FromSeconds(20));
            }
            catch (TimeoutException)
            {
                return "session did not finish within 20s after the provider dropped";
            }

            if (FakeWssClient.ConnectCallCount != 1)
                return $"ConnectCallCount was {FakeWssClient.ConnectCallCount} after teardown, expected 1 (an auto-reconnect appeared)";

            if (FakeWssClient.DisconnectCallCount != 0)
                return $"DisconnectCallCount was {FakeWssClient.DisconnectCallCount}, expected 0 (redundant close on an already-closed socket)";

            if (string.IsNullOrEmpty(endedSessionId))
                return "OnSessionEndedAsync never ran";

            try
            {
                ClientAdapter.Received().BuildErrorMessage("ConnectionLost", Arg.Is<string>(s => s.Contains("server closed")), Arg.Any<string>());
            }
            catch (Exception ex)
            {
                return $"client was not notified of ConnectionLost: {ex.GetType().Name}";
            }

            return null;
        }
    }
}
