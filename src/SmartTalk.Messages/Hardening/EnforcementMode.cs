namespace SmartTalk.Messages.Hardening;

/// <summary>
/// Three-mode enforcement for hardening checks (Rule 11 of the project style guide).
/// Every safety/correctness check that could break existing deploys if it became the
/// default reads its mode from a single env var. The default in code is <see cref="Warn"/>,
/// preserving backward compatibility while surfacing the tech debt in structured logs.
/// </summary>
public enum EnforcementMode
{
    /// <summary>
    /// Silent allow. The check does not run; invalid values pass through with no log.
    /// Reserved for dev, tests, and explicit operator opt-out / emergency rollback.
    /// </summary>
    Off,

    /// <summary>
    /// Allow with structured warning. Invalid values pass through but emit a Serilog
    /// warning naming both the offending value and the env-var name needed to switch
    /// to <see cref="Strict"/>. Used as the default to preserve backward compatibility.
    /// </summary>
    Warn,

    /// <summary>
    /// Reject (throw) with an actionable error message. Operators opt in for production
    /// hardening; a future major release flips this to the default.
    /// </summary>
    Strict
}
