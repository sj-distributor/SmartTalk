namespace SmartTalk.Messages.Enums.RealtimeAi;

/// <summary>
/// Why a realtime session ended. Low-cardinality by design so it can be faceted and alerted on:
/// "how many calls did we lose to the provider yesterday" has to be one Seq query, not a scan.
///
/// <para>Before this existed the engine reported both a designed timeout and its own decision to
/// tear down after a provider fault as "Client disconnected abnormally", which pointed incident
/// response at the telephony vendor for a failure the server had chosen.</para>
/// </summary>
public enum RealtimeAiSessionOutcome
{
    /// <summary>The client closed cleanly — a caller hanging up. The normal ending.</summary>
    ClientClosed,

    /// <summary>The session hit its configured duration ceiling.</summary>
    MaxDurationReached,

    /// <summary>The engine tore the session down after a critical provider error.</summary>
    ProviderFault,

    /// <summary>The client connection dropped without a close frame.</summary>
    ClientAborted
}
