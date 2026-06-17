namespace SmartTalk.Core.Services.RealtimeAiV2.Watchdog;

/// <summary>
/// Operator-tunable backstop timeout for the realtime turn lifecycle. Read from an environment variable
/// (so air-gapped / fork operators can tune it without a code change) with a compile-time default; the
/// env-var name is pinned by a unit test, so renaming it is a compile-visible decision rather than a
/// silent break for an operator who set the old name.
/// </summary>
public static class RealtimeAiTurnWatchdogDefaults
{
    public const string TtsSynthesisTimeoutEnvVar = "SMARTTALK_REALTIME_TTS_SYNTHESIS_TIMEOUT_MS";

    public const int DefaultTtsSynthesisTimeoutMs = 8000;

    public const string TurnHardCeilingEnvVar = "SMARTTALK_REALTIME_TURN_HARD_CEILING_MS";

    public const int DefaultTurnHardCeilingMs = 45000;

    public static TimeSpan TtsSynthesisTimeout => Resolve(TtsSynthesisTimeoutEnvVar, DefaultTtsSynthesisTimeoutMs);

    public static TimeSpan TurnHardCeiling => Resolve(TurnHardCeilingEnvVar, DefaultTurnHardCeilingMs);

    private static TimeSpan Resolve(string envVar, int defaultMs) =>
        TimeSpan.FromMilliseconds(
            int.TryParse(Environment.GetEnvironmentVariable(envVar), out var ms) && ms > 0 ? ms : defaultMs);
}
