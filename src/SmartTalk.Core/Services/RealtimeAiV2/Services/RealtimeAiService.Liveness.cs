using System.Diagnostics;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2.Liveness;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    /// <summary>Test-only seams so a test need not wait the real thresholds. Null in production.</summary>
    internal TimeSpan? ProviderSilenceThresholdOverride { get; set; }

    internal TimeSpan? ListeningSilenceThresholdOverride { get; set; }

    internal TimeSpan? FunctionCallStillRunningThresholdOverride { get; set; }

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
        var inResponseThreshold = ProviderSilenceThresholdOverride ?? RealtimeAiLivenessDefaults.InResponse;
        var listeningThreshold = ListeningSilenceThresholdOverride ?? RealtimeAiLivenessDefaults.WhileListening;
        var functionCallThreshold = FunctionCallStillRunningThresholdOverride ?? RealtimeAiLivenessDefaults.FunctionCallStillRunning;
        var interval = ProviderLivenessPollIntervalOverride ?? RealtimeAiLivenessDefaults.PollInterval;

        // Reported once per gap, not once per poll, and tracked per window: a wedged connection would
        // otherwise emit a line every interval for the rest of the call, and one window's report must
        // not suppress the other's.
        var reportedInResponseGap = false;
        var reportedListeningGap = false;
        var reportedRunningFunctionCall = false;

        while (IsProviderSessionActive)
        {
            try
            {
                await Task.Delay(interval, _ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Break rather than return, so a handler still holding the loop when the caller hangs
                // up gets its duration recorded. Returning here discarded exactly the number this
                // observer exists to produce: the completion line only reports handlers that returned.
                break;
            }

            var silence = Stopwatch.GetElapsedTime(Interlocked.Read(ref _ctx.LastProviderMessageAt));

            reportedInResponseGap = ObserveGap(
                _ctx.IsProviderResponseInProgress, silence, inResponseThreshold, reportedInResponseGap,
                "[RealtimeAi] Provider silent while a response was in flight, SessionId: {SessionId}, GapMs: {GapMs}, Round: {Round}");

            // The listening window: the provider said the caller started speaking and has produced
            // nothing of its own since. Its own template so the two are separable in Seq — they mean
            // different faults and the operator response differs.
            reportedListeningGap = ObserveGap(
                _ctx.IsAwaitingProviderResponse, silence, listeningThreshold, reportedListeningGap,
                "[RealtimeAi] No provider traffic while awaiting a response, SessionId: {SessionId}, GapMs: {GapMs}, Round: {Round}");

            reportedRunningFunctionCall = ObserveRunningFunctionCall(functionCallThreshold, reportedRunningFunctionCall);
        }

        ReportFunctionCallStillRunningAtTeardown();
    }

    /// <summary>
    /// Records a tool handler that is still holding the provider receive loop. Its own template rather
    /// than the gap one: a slow tool filed under GapMs is indistinguishable from a provider that went
    /// quiet, and the two mean different faults.
    ///
    /// <para>Record-only, like everything else here. The turn ceiling deliberately stands down while a
    /// handler runs — bounding the WAIT without bounding the WORK would leave the caller's microphone
    /// suspended by an abandoned handler while the turn completes and the idle timer schedules a
    /// hangup, which is worse than the unbounded handler it replaces.</para>
    /// </summary>
    private bool ObserveRunningFunctionCall(TimeSpan threshold, bool alreadyReported)
    {
        if (_ctx.CurrentFunctionCall is not { } running) return false;

        var elapsed = Stopwatch.GetElapsedTime(running.StartedAt);

        if (elapsed < threshold) return false;

        if (alreadyReported) return true;

        Log.Warning(
            "[RealtimeAi] Function call still holding the receive loop, SessionId: {SessionId}, FunctionName: {FunctionName}, ElapsedFunctionCallMs: {ElapsedFunctionCallMs}, Round: {Round}",
            _ctx.SessionId, running.FunctionName, (long)elapsed.TotalMilliseconds, _ctx.Round);

        return true;
    }

    /// <summary>
    /// The tail. A handler still running when the session ends never reaches the completion line, so
    /// without this the one run worth measuring is the one that leaves no duration behind at all.
    /// </summary>
    private void ReportFunctionCallStillRunningAtTeardown()
    {
        if (_ctx.CurrentFunctionCall is not { } running) return;

        Log.Warning(
            "[RealtimeAi] Session ended with a function call still running, SessionId: {SessionId}, FunctionName: {FunctionName}, ElapsedFunctionCallMs: {ElapsedFunctionCallMs}, Round: {Round}",
            _ctx.SessionId, running.FunctionName, (long)Stopwatch.GetElapsedTime(running.StartedAt).TotalMilliseconds, _ctx.Round);
    }

    /// <summary>
    /// Records one window's gap if it is open and over threshold, and returns whether this gap has now
    /// been reported. Records only — the caller's call is never worth a threshold nobody has measured.
    /// </summary>
    private bool ObserveGap(bool windowIsOpen, TimeSpan silence, TimeSpan threshold, bool alreadyReported, string template)
    {
        if (!windowIsOpen || silence < threshold) return false;

        if (alreadyReported) return true;

        Log.Warning(template, _ctx.SessionId, (long)silence.TotalMilliseconds, _ctx.Round);

        return true;
    }
}
