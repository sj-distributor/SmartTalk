namespace SmartTalk.Core.Logging;

/// <summary>
/// Well-known ambient log property names.
///
/// <para>These are a wire contract, not implementation detail: they are what Seq queries, saved
/// signals and dashboards filter on, and they are pushed by one layer and read by another. Renaming
/// one silently breaks every stored query, so each is pinned by a literal-value test.</para>
///
/// <para>Kept out of any one feature's namespace so a consumer does not have to reference an
/// engine's concrete service class just to name the property it correlates on.</para>
/// </summary>
public static class LogProperties
{
    /// <summary>Correlates every line of one realtime session — consumer, engine, and transport.</summary>
    public const string RealtimeSessionId = "RealtimeSessionId";

    /// <summary>Telephony call identifier. Known only once the transport reports it mid-session.</summary>
    public const string CallSid = "CallSid";

    /// <summary>Telephony media-stream identifier, same timing as <see cref="CallSid"/>.</summary>
    public const string StreamSid = "StreamSid";

    public const string AgentId = "AgentId";

    public const string AssistantId = "AssistantId";
}
