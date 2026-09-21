namespace SmartTalk.Core.Services.RealtimeAiV2.Watchdog;

/// <summary>
/// Fixed backstop durations for the realtime turn lifecycle. These are internal engineering safety limits,
/// deliberately NOT configurable — a wedged-turn backstop is not something an operator tunes per deployment,
/// so there is no environment variable or session-option surface for them. Tests inject a shorter value via
/// <c>RealtimeAiService</c>'s internal override seam, never through configuration.
/// </summary>
public static class RealtimeAiTurnWatchdogDefaults
{
    /// <summary>
    /// Max time to wait for an external TTS provider to signal synthesis completion after the inference
    /// provider's turn is already done, before the engine force-completes the turn.
    /// </summary>
    public static readonly TimeSpan TtsSynthesisTimeout = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Absolute lifetime of any single turn, in either output mode, from the response starting. Covers
    /// a provider that stalls without ever sending response.done — including a half-open connection,
    /// where nothing else in the engine notices.
    ///
    /// <para>Set far above any plausible turn rather than tuned to observed latency: two minutes
    /// cannot be reached by a legitimate response, so this can only fire on a turn that is already
    /// broken. A tighter bound is worth having once there are measurements to derive it from —
    /// guessing one risks cutting off real speech.</para>
    /// </summary>
    public static readonly TimeSpan TurnHardCeiling = TimeSpan.FromSeconds(120);
}
