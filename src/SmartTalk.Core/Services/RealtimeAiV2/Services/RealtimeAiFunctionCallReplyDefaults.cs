namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

/// <summary>
/// Bounds on how often the engine answers the model about a tool that failed.
/// </summary>
public static class RealtimeAiFunctionCallReplyDefaults
{
    /// <summary>
    /// The most failure replies one session will ever send, across all tools.
    ///
    /// <para>Per-tool alone is not a bound: seventeen tools are reachable on the phone path, so
    /// seventeen distinct failures would still be seventeen extra turns. Each answered failure
    /// completes a turn, and starting the idle timer stops it first — so the 60-second countdown
    /// restarts from zero on every one, and the phone path sets no session ceiling behind it. Keeping
    /// the worst case to a handful is the safety property, not a tuning knob.</para>
    /// </summary>
    public const int MaxFailureRepliesPerSession = 3;

    /// <summary>
    /// What the model is told. Steers it back to the customer rather than into another attempt at the
    /// same tool: a wording that reads as "retry" produces a tight, audio-free exchange the caller
    /// hears as silence.
    /// </summary>
    public const string FailureReplyOutput =
        "That request did not complete. Apologise briefly in the customer's language and ask them to confirm what they need. " +
        "Do not call this tool again unless the customer asks for it a second time.";
}
