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
    /// Absolute lifetime of a single external-TTS (text-output) turn from its first text, before the engine
    /// force-completes it (covers a provider that streams text then stalls without ever sending response.done).
    /// </summary>
    public static readonly TimeSpan TurnHardCeiling = TimeSpan.FromSeconds(45);
}
