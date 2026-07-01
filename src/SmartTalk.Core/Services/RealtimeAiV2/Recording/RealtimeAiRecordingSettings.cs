namespace SmartTalk.Core.Services.RealtimeAiV2.Recording;

/// <summary>
/// Picks the recording buffer implementation for a session based on env vars.
/// Defaults preserve the previous unbounded behaviour exactly — operators must
/// explicitly opt in to rolling-window mode.
///
/// <para>Renaming either env var literal breaks every operator who pinned a
/// custom value, so both literals are hard-pinned in
/// <c>RealtimeAiRecordingSettingsTests</c> (Rule 8).</para>
/// </summary>
public static class RealtimeAiRecordingSettings
{
    /// <summary>Selects the buffer implementation. Values: "unbounded" (default) or "rolling".</summary>
    public const string BufferModeEnvVar = "SQUID_SMARTTALK_RECORDING_BUFFER_MODE";

    /// <summary>Window size when mode = rolling. Range 30–3600 seconds; out-of-range falls back to default 300.</summary>
    public const string BufferSecondsEnvVar = "SQUID_SMARTTALK_RECORDING_BUFFER_SECONDS";

    /// <summary>24kHz mono S16LE → 48,000 byte/s. Matches AudioCodecConverter.RecordingSampleRate.</summary>
    private const int PcmBytesPerSecond = 24_000 * 2;

    private const int DefaultSeconds = 300;
    private const int MinSeconds = 30;
    private const int MaxSeconds = 3_600;

    public enum BufferMode
    {
        Unbounded,
        Rolling
    }

    /// <summary>Reads <see cref="BufferModeEnvVar"/> at call time.</summary>
    public static BufferMode ResolveMode() =>
        ParseMode(Environment.GetEnvironmentVariable(BufferModeEnvVar));

    /// <summary>Reads <see cref="BufferSecondsEnvVar"/> at call time. Bounded to [Min,Max] with default fallback.</summary>
    public static int ResolveSeconds() =>
        ParseSeconds(Environment.GetEnvironmentVariable(BufferSecondsEnvVar));

    /// <summary>
    /// Constructs the buffer for a fresh session — call site is
    /// <c>RealtimeAiService.BuildRecordingIfRequired</c>.
    /// </summary>
    public static IRecordingBuffer Create()
    {
        if (ResolveMode() == BufferMode.Unbounded) return new UnboundedMemoryBuffer();

        var capacityBytes = ResolveSeconds() * PcmBytesPerSecond;

        return new RollingWindowBuffer(capacityBytes);
    }

    /// <summary>Pure parser — exposed for unit tests to avoid env var mutation.</summary>
    public static BufferMode ParseMode(string raw) =>
        string.Equals(raw?.Trim(), "rolling", StringComparison.OrdinalIgnoreCase)
            ? BufferMode.Rolling
            : BufferMode.Unbounded;

    /// <summary>Pure parser — exposed for unit tests.</summary>
    public static int ParseSeconds(string raw)
    {
        if (int.TryParse(raw, out var seconds) && seconds is >= MinSeconds and <= MaxSeconds)
            return seconds;

        return DefaultSeconds;
    }
}
