namespace SmartTalk.Core.Services.RealtimeAiV2.Wss;

/// <summary>
/// Shared settings for the realtime provider WebSocket clients (OpenAI, Google).
///
/// <para>The keep-alive interval is tighter than .NET's 30-second default
/// because the realtime APIs send no traffic during AI-silent windows
/// (e.g. while the caller is reading the menu). Corporate proxies and
/// cloud LBs that idle-out long-lived TLS at ~30s have been observed
/// to drop the connection mid-call.</para>
///
/// <para>Operators can override via env var. Renaming
/// <see cref="KeepAliveSecondsEnvVar"/> breaks every air-gapped operator
/// who pinned a custom value, so the literal is hard-pinned in
/// <c>RealtimeAiWebSocketSettingsTests</c>.</para>
/// </summary>
public static class RealtimeAiWebSocketSettings
{
    /// <summary>
    /// Env var operators set to override the keep-alive interval (in seconds).
    /// Valid range: 5–120. Out-of-range or unparseable values fall back to the default.
    /// </summary>
    public const string KeepAliveSecondsEnvVar = "SQUID_SMARTTALK_REALTIME_WS_KEEPALIVE_SECONDS";

    private static readonly TimeSpan DefaultKeepAlive = TimeSpan.FromSeconds(15);
    private const int MinSeconds = 5;
    private const int MaxSeconds = 120;

    /// <summary>
    /// Returns the keep-alive interval, reading from <see cref="KeepAliveSecondsEnvVar"/>
    /// at call time. Each new wss client gets the current env value (no caching, since
    /// changes during process lifetime are rare and the call cost is trivial).
    /// </summary>
    public static TimeSpan ResolveKeepAliveInterval() =>
        Parse(Environment.GetEnvironmentVariable(KeepAliveSecondsEnvVar));

    /// <summary>
    /// Pure parser — exposed for unit tests so we don't have to mutate process env vars.
    /// </summary>
    public static TimeSpan Parse(string raw)
    {
        if (int.TryParse(raw, out var seconds) && seconds is >= MinSeconds and <= MaxSeconds)
            return TimeSpan.FromSeconds(seconds);

        return DefaultKeepAlive;
    }
}
