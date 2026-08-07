using System.Diagnostics;
using Serilog;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    /// <summary>
    /// How long the provider may stay silent mid-response before the gap is worth recording. Not a
    /// timeout — nothing acts on it. See <see cref="RunProviderLivenessObserverAsync"/>.
    /// </summary>
    private static readonly TimeSpan ProviderSilenceObservationThreshold = TimeSpan.FromSeconds(20);

    private static readonly TimeSpan ProviderLivenessPollInterval = TimeSpan.FromSeconds(2);

    /// <summary>Test-only seams so a test need not wait the real threshold. Null in production.</summary>
    internal TimeSpan? ProviderSilenceThresholdOverride { get; set; }

    internal TimeSpan? ProviderLivenessPollIntervalOverride { get; set; }

    /// <summary>
    /// Watches for the provider going quiet while a response is supposedly streaming, and RECORDS it.
    /// Deliberately takes no action.
    ///
    /// <para>A half-open TCP connection — a firewall silently dropping the state table, no FIN, no RST
    /// — leaves <c>ReceiveAsync</c> parked forever while the socket still reports Open, so nothing in
    /// the engine notices. On the built-in audio path, which is every phone call today, no turn
    /// watchdog arms either, and the caller sits in silence on a live billed call until TCP retry
    /// finally exhausts, roughly fifteen minutes later.</para>
    ///
    /// <para>The obvious fix — raise ConnectionLost on silence — is the dangerous one to guess at.
    /// ConnectionLost is classified critical, so it hangs up on the caller; a threshold set even
    /// slightly too low would manufacture more dropped calls than the fault it targets. Nobody knows
    /// what the real distribution of mid-response provider gaps looks like on this traffic. So this
    /// measures it first, and a later change decides what to do with a threshold derived from the
    /// answer rather than from intuition.</para>
    ///
    /// <para>Gated on a response being in flight: silence between turns is a caller thinking, and
    /// alerting on it would bury the signal that matters.</para>
    /// </summary>
    private void StartProviderLivenessObserver()
    {
        _ctx.LastProviderMessageAt = Stopwatch.GetTimestamp();

        _ = RunProviderLivenessObserverAsync();
    }

    private async Task RunProviderLivenessObserverAsync()
    {
        var threshold = ProviderSilenceThresholdOverride ?? ProviderSilenceObservationThreshold;
        var interval = ProviderLivenessPollIntervalOverride ?? ProviderLivenessPollInterval;
        var reportedForCurrentGap = false;

        while (IsProviderSessionActive)
        {
            try
            {
                await Task.Delay(interval, _ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_ctx.IsProviderResponseInProgress)
            {
                reportedForCurrentGap = false;
                continue;
            }

            var silence = Stopwatch.GetElapsedTime(Interlocked.Read(ref _ctx.LastProviderMessageAt));

            if (silence < threshold)
            {
                reportedForCurrentGap = false;
                continue;
            }

            // Once per gap, not once per poll: a genuinely wedged connection would otherwise emit a
            // line every interval for the rest of the call.
            if (reportedForCurrentGap) continue;

            reportedForCurrentGap = true;

            Log.Warning(
                "[RealtimeAi] Provider silent while a response was in flight, SessionId: {SessionId}, GapMs: {GapMs}, Round: {Round}",
                _ctx.SessionId, (long)silence.TotalMilliseconds, _ctx.Round);
        }
    }
}
