using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2.Watchdog;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    // Backstop for the external-TTS wedge: the inference provider's turn is done but the TTS provider
    // never raises SynthesisCompleted/Failed, so the dual gate would wait forever. Armed only on the
    // external-TTS waiting path; built-in audio mode completes on provider-done and never arms.
    private void ArmTtsSynthesisWatchdog()
    {
        var generation = Interlocked.Read(ref _ctx.CurrentTurnGeneration);
        var timeout = _ctx.Options.TtsSynthesisTimeout ?? RealtimeAiTurnWatchdogDefaults.TtsSynthesisTimeout;

        _ = RunTtsSynthesisWatchdogAsync(generation, timeout);
    }

    private async Task RunTtsSynthesisWatchdogAsync(long generation, TimeSpan timeout)
    {
        try
        {
            await Task.Delay(timeout, _ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;   // session ended before the ceiling — nothing to force
        }

        await ForceTtsSynthesisCompletionIfStillWaitingAsync(generation).ConfigureAwait(false);
    }

    private async Task ForceTtsSynthesisCompletionIfStillWaitingAsync(long generation)
    {
        if (!IsProviderSessionActive) return;

        var token = _ctx.SessionCts?.Token ?? CancellationToken.None;
        var shouldComplete = false;

        await _ctx.TurnCompletionStateLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            // A newer turn started (superseded), or the turn already completed / the real synthesis signal
            // arrived — stand down. The same exactly-once gate the real signal uses guards us here too.
            if (Interlocked.Read(ref _ctx.CurrentTurnGeneration) != generation) return;
            if (_ctx.CurrentResponseTurnCompletedHandled || _ctx.CurrentResponseTtsSynthesisCompleted) return;

            _ctx.CurrentResponseTtsSynthesisCompleted = true;
            shouldComplete = TryMarkCurrentResponseTurnCompletedLocked();
        }
        finally
        {
            _ctx.TurnCompletionStateLock.Release();
        }

        if (!shouldComplete) return;

        Log.Warning("[RealtimeAi] TTS synthesis watchdog fired — forced turn completion, SessionId: {SessionId}", _ctx.SessionId);

        await OnAiTurnCompletedAsync().ConfigureAwait(false);
    }

    // Absolute backstop for an external (text) turn: armed at its first text. Covers the provider that
    // streams text then stalls without ever sending response.done — the TTS-synthesis watchdog never arms
    // there (it arms on provider-done), so without this the turn would hang forever.
    private void ArmTurnHardCeilingWatchdog()
    {
        var generation = Interlocked.Read(ref _ctx.CurrentTurnGeneration);
        var ceiling = _ctx.Options.TurnHardCeiling ?? RealtimeAiTurnWatchdogDefaults.TurnHardCeiling;

        _ = RunTurnHardCeilingWatchdogAsync(generation, ceiling);
    }

    private async Task RunTurnHardCeilingWatchdogAsync(long generation, TimeSpan ceiling)
    {
        try
        {
            await Task.Delay(ceiling, _ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await ForceTurnCompletionAtHardCeilingAsync(generation).ConfigureAwait(false);
    }

    private async Task ForceTurnCompletionAtHardCeilingAsync(long generation)
    {
        if (!IsProviderSessionActive) return;

        var token = _ctx.SessionCts?.Token ?? CancellationToken.None;
        var shouldComplete = false;

        await _ctx.TurnCompletionStateLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (Interlocked.Read(ref _ctx.CurrentTurnGeneration) != generation) return;
            if (_ctx.CurrentResponseTurnCompletedHandled) return;   // already completed via the gate

            // Force BOTH gate legs: the provider may have stalled before response.done, and the TTS may
            // still be mid-synthesis. The handled latch (waitsForExternalTts is true here) keeps it exactly-once.
            _ctx.CurrentResponseProviderTurnCompleted = true;
            _ctx.CurrentResponseTtsSynthesisCompleted = true;
            shouldComplete = TryMarkCurrentResponseTurnCompletedLocked();
        }
        finally
        {
            _ctx.TurnCompletionStateLock.Release();
        }

        if (!shouldComplete) return;

        Log.Warning("[RealtimeAi] Turn hard ceiling reached — forced turn completion, SessionId: {SessionId}", _ctx.SessionId);

        await OnAiTurnCompletedAsync().ConfigureAwait(false);
    }
}
