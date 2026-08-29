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

    /// <summary>
    /// How long a tool handler may hold the provider receive loop before the run is worth recording.
    ///
    /// <para>A LOG threshold, not a control threshold — which is why it can be picked without
    /// production data. Nothing acts on it: too high costs one missing line, too low costs one Warning
    /// on a healthy call. Neither costs a call.</para>
    ///
    /// <para>Set above every budget the slow path believes it holds — the MiniMax synthesizer grants
    /// itself 90 seconds on top of a 10-second idle timeout, with a text leg before it and a fallback
    /// leg after — so a line means "this run is outside what anything thinks it is entitled to". It
    /// matches the turn ceiling deliberately: the moment the ceiling stands down for a running tool is
    /// the moment that tool becomes worth recording.</para>
    ///
    /// <para>It is NOT a p99 and must never be reused as the value of a bound. Nobody has a latency
    /// distribution for the slow handler — producing one is what this exists for.</para>
    /// </summary>
    public static readonly TimeSpan FunctionCallStillRunning = TimeSpan.FromSeconds(120);

    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
}
