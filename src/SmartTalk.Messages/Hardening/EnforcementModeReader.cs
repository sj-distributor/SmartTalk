namespace SmartTalk.Messages.Hardening;

/// <summary>
/// Parses a single env var into <see cref="EnforcementMode"/>. Accepts the canonical
/// names (<c>off</c>, <c>warn</c>, <c>strict</c>) plus a small set of operator-friendly
/// aliases that map to the same mode.
///
/// <para>
/// Air-gapped / fork operators need to be able to flip one env var to change behaviour
/// without redeploying code. The single-env-var contract is what makes that possible —
/// changing the env-var name in code is a hard-pinned compile-time decision (Rule 8).
/// </para>
/// </summary>
public static class EnforcementModeReader
{
    /// <summary>
    /// Reads the env var named <paramref name="envVarName"/> and returns the matching
    /// <see cref="EnforcementMode"/>. Unrecognised or unset values return
    /// <paramref name="defaultMode"/> (defaulting to <see cref="EnforcementMode.Warn"/>
    /// per Rule 11 — Warn preserves backward compatibility).
    /// </summary>
    public static EnforcementMode Read(string envVarName, EnforcementMode defaultMode = EnforcementMode.Warn)
    {
        var raw = Environment.GetEnvironmentVariable(envVarName);

        if (string.IsNullOrWhiteSpace(raw)) return defaultMode;

        return Parse(raw.Trim(), defaultMode);
    }

    /// <summary>
    /// Pure parser exposed for unit testability. Same aliases as <see cref="Read"/>.
    /// </summary>
    public static EnforcementMode Parse(string raw, EnforcementMode defaultMode = EnforcementMode.Warn) =>
        raw.ToLowerInvariant() switch
        {
            "off" or "disabled" or "0" or "false" or "no" => EnforcementMode.Off,
            "warn" or "warning"                            => EnforcementMode.Warn,
            "strict" or "enforce" or "1" or "true" or "yes" => EnforcementMode.Strict,
            _                                              => defaultMode
        };
}
