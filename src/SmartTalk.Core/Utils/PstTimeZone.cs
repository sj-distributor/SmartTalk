namespace SmartTalk.Core.Utils;

/// <summary>
/// Resolves the Pacific Standard Time zone once per process and caches the result.
///
/// <para>Why this exists:</para>
/// <list type="bullet">
///   <item>The zone is needed in multiple V2 code paths
///         (<c>CheckIfInServiceHours</c>, <c>ResolveStaticPromptVariables</c>).
///         Centralizing the magic string prevents typos like <c>"PST"</c> or
///         <c>"Pacific"</c>.</item>
///   <item>.NET 6+ converts between Windows IDs ("Pacific Standard Time") and
///         IANA IDs ("America/Los_Angeles") automatically when the requested
///         form is not present on the host. We additionally try the IANA form
///         as defense-in-depth in case ICU is misconfigured.</item>
///   <item>.NET 8 caches <see cref="TimeZoneInfo"/> by ID internally, but
///         using <see cref="Lazy{T}"/> here also serves as a clear,
///         eager-fail point with an actionable error message.</item>
/// </list>
/// </summary>
public static class PstTimeZone
{
    /// <summary>Windows-style ID, the historical default for this codebase.</summary>
    public const string WindowsId = "Pacific Standard Time";

    /// <summary>IANA-style ID used as a fallback if the Windows ID cannot be resolved.</summary>
    public const string IanaId = "America/Los_Angeles";

    private static readonly Lazy<TimeZoneInfo> Lazy = new(ResolveOrThrow);

    /// <summary>
    /// Returns the cached Pacific Standard Time zone. Thread-safe; the underlying
    /// resolution runs at most once per process. If resolution fails, the cached
    /// failure is rethrown on every subsequent access (this is an unrecoverable
    /// host configuration error).
    /// </summary>
    public static TimeZoneInfo Get() => Lazy.Value;

    private static TimeZoneInfo ResolveOrThrow()
    {
        if (TryResolve(WindowsId, out var byWindows)) return byWindows;
        if (TryResolve(IanaId, out var byIana)) return byIana;

        throw new InvalidOperationException(
            $"Cannot resolve Pacific Standard Time on this host. " +
            $"Tried '{WindowsId}' and '{IanaId}'. " +
            $"Ensure tzdata is installed (Linux) or ICU support is available.");
    }

    private static bool TryResolve(string id, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            zone = null!;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            zone = null!;
            return false;
        }
    }
}
