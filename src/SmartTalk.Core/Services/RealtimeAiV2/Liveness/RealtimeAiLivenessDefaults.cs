namespace SmartTalk.Core.Services.RealtimeAiV2.Liveness;

/// <summary>
/// How long the provider may stay quiet before the gap is worth recording. Neither value is a
/// timeout — nothing acts on either, by design: raising ConnectionLost on silence is classified
/// critical and hangs up on the caller, so a threshold guessed slightly low would manufacture more
/// dropped calls than the fault it targets.
/// </summary>
public static class RealtimeAiLivenessDefaults
{
    /// <summary>Silence while a response is supposedly streaming — the provider owes audio right now.</summary>
    public static readonly TimeSpan InResponse = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Silence after the provider reported the caller started speaking and before it produced anything
    /// of its own. Deliberately far above <see cref="InResponse"/>: a caller reading an order aloud
    /// still produces transcription frames, which keep the gap reset, so this only grows when the
    /// provider sends literally nothing. No legitimate call does that for this long, and being wrong
    /// costs one Warning on a healthy call rather than the call itself.
    /// </summary>
    public static readonly TimeSpan WhileListening = TimeSpan.FromSeconds(45);

    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
}
