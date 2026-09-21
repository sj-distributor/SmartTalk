using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2.Watchdog;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    // Test-only seam (internal, not part of any public/operator config surface): overrides the fixed
    // RealtimeAiTurnWatchdogDefaults durations so tests need not wait the real 8s/45s ceiling. Null in
    // production → the constants apply. Per-instance, so concurrent test classes never collide.
    internal TimeSpan? TtsSynthesisWatchdogTimeoutOverride { get; set; }
    internal TimeSpan? TurnHardCeilingWatchdogOverride { get; set; }

    // Backstop for the external-TTS wedge: the inference provider's turn is done but the TTS provider
    // never raises SynthesisCompleted/Failed, so the dual gate would wait forever. Armed only on the
    // external-TTS waiting path; built-in audio mode completes on provider-done and never arms.
    private void ArmTtsSynthesisWatchdog()
    {
        var generation = Interlocked.Read(ref _ctx.CurrentTurnGeneration);
        var timeout = TtsSynthesisWatchdogTimeoutOverride ?? RealtimeAiTurnWatchdogDefaults.TtsSynthesisTimeout;

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

    // Absolute backstop for a turn: armed when a response starts, and additionally at first text on the
    // external-TTS path, where the provider may stream text and then stall without ever sending
    // response.done and the TTS-synthesis watchdog never arms (it arms on provider-done).
    //
    // Re-armed rather than fired while the engine's own function-call handlers hold the receive loop —
    // see ForceTurnCompletionAtHardCeilingAsync.
    private void ArmTurnHardCeilingWatchdog()
    {
        var generation = Interlocked.Read(ref _ctx.CurrentTurnGeneration);
        var ceiling = TurnHardCeilingWatchdogOverride ?? RealtimeAiTurnWatchdogDefaults.TurnHardCeiling;

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

        // The bound means "the provider went quiet", never "our own tool is still working". Handlers run
        // inline on the provider receive loop, which cannot hold back this timer, so without this a
        // healthy turn whose tool is merely slow gets closed behind its back: barge-in loses the two
        // fields it needs and the assistant talks over the caller for the rest of the turn, Round
        // advances so the follow-up and auto-hangup arm early, and the idle timer starts — whose
        // default handling schedules the job that hangs up the call.
        if (_ctx.IsRunningFunctionCallHandlers)
        {
            ArmTurnHardCeilingWatchdog();

            return;
        }

        var token = _ctx.SessionCts?.Token ?? CancellationToken.None;
        var shouldComplete = false;

        await _ctx.TurnCompletionStateLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (Interlocked.Read(ref _ctx.CurrentTurnGeneration) != generation) return;
            if (_ctx.CurrentResponseTurnCompletedHandled) return;   // already completed via the gate

            // Audio mode has no handled latch, so this is what stops a ceiling from adding a second
            // completion behind a turn that already finished on its own.
            if (Interlocked.Read(ref _ctx.NormallyCompletedTurnGeneration) == generation) return;

            // Two ceilings can be armed for one external-TTS turn (response.created and its first
            // text); only the first may close it.
            if (Interlocked.Read(ref _ctx.ForceCompletedTurnGeneration) == generation) return;

            // Force BOTH gate legs: the provider may have stalled before response.done, and the TTS may
            // still be mid-synthesis. Audio mode has no handled latch, so exactly-once there comes from
            // the two generation stamps above, not from the latch.
            _ctx.CurrentResponseProviderTurnCompleted = true;
            _ctx.CurrentResponseTtsSynthesisCompleted = true;
            shouldComplete = TryMarkCurrentResponseTurnCompletedLocked();

            if (shouldComplete) Interlocked.Exchange(ref _ctx.ForceCompletedTurnGeneration, generation);
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
