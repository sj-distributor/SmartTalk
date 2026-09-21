using System.Diagnostics;
using Serilog;

namespace SmartTalk.Core.Services.Ffmpeg;

/// <summary>
/// Puts a ceiling on how long an ffmpeg child may hold the caller's thread, and guarantees the child
/// is dead before the caller unlinks its temp files.
///
/// <para>Used by <c>FfmpegService.ConvertWavToULawAsync</c> only. That is the one ffmpeg call on the
/// live-call path: the repeat-order handler runs inline on the provider receive loop with the caller's
/// microphone suspended, so a wedged ffmpeg means the customer is muted and the engine deaf until they
/// hang up. The other ffmpeg call sites are Hangfire jobs or unreachable code and are deliberately left
/// unbounded — see the comment at the <c>ConvertWavToULawAsync</c> call site.</para>
///
/// <para>Unlinking the temp files does <b>not</b> stop ffmpeg: measured on Linux, it keeps its fds on
/// the deleted inodes, keeps burning CPU, and exits 0 while writing into an inode nothing can reach.
/// Only the kill reclaims the process and the disk, which is why it has to happen here rather than in
/// the caller's finally.</para>
///
/// <para>Renaming <see cref="UlawBoundSecondsEnvVar"/> breaks every operator who pinned a custom value,
/// so the literal is hard-pinned in <c>FfmpegProcessBoundTests</c> (Rule 8).</para>
/// </summary>
public static class FfmpegProcessBound
{
    /// <summary>Ceiling for the µ-law conversion. Range 5–300 seconds; out-of-range falls back to 30.</summary>
    public const string UlawBoundSecondsEnvVar = "SQUID_SMARTTALK_FFMPEG_ULAW_BOUND_SECONDS";

    /// <summary>
    /// 30s is a backstop against a wedged process, not a latency target. Measured worst case on one
    /// throttled Linux core is 16.9s for a 30-minute input; the real input is gpt-audio's spoken
    /// order readback (<c>OpenaiClient.GenerateAudioChatCompletionAsync</c>), which measures ~0.5s.
    /// It also sits below the 60s idle-follow-up default so recovery lands before any hangup logic.
    /// </summary>
    private const int DefaultSeconds = 30;
    private const int MinSeconds = 5;
    private const int MaxSeconds = 300;

    /// <summary>
    /// Ceiling for the post-kill reap. Measured at 24ms against real ffmpeg, so 2s is ~80x headroom.
    /// It exists because <see cref="Process.WaitForExitAsync"/> also waits for the redirected pipes to
    /// reach EOF, and that half is not covered by the token — a descendant holding those pipes makes it
    /// hang forever (measured). Today's ffmpeg spawns no children, but a method whose whole purpose is
    /// removing an unbounded wait from the receive loop must not add one in its own recovery path.
    /// </summary>
    internal static readonly TimeSpan ReapBound = TimeSpan.FromSeconds(2);

    /// <summary>Reads <see cref="UlawBoundSecondsEnvVar"/> at call time.</summary>
    public static TimeSpan ResolveUlawBound() =>
        TimeSpan.FromSeconds(ParseSeconds(Environment.GetEnvironmentVariable(UlawBoundSecondsEnvVar)));

    /// <summary>Pure parser — exposed for unit tests to avoid env var mutation.</summary>
    public static int ParseSeconds(string raw)
    {
        if (int.TryParse(raw, out var seconds) && seconds is >= MinSeconds and <= MaxSeconds) return seconds;

        return DefaultSeconds;
    }

    /// <summary>
    /// Waits for <paramref name="proc"/> for at most <paramref name="bound"/>. Returns true on a clean
    /// exit — the child is untouched and its ExitCode is the caller's to read. Returns false when the
    /// bound fired, having killed and reaped the child first.
    ///
    /// <para>Never throws on its own account. It rethrows only genuine cancellation of
    /// <paramref name="cancellationToken"/>, which is exactly what the bare <c>WaitForExitAsync</c> it
    /// replaces already did — and now kills the child on that path too, instead of leaking it.</para>
    /// </summary>
    public static async Task<bool> TryWaitForExitWithinAsync(Process proc, TimeSpan bound, CancellationToken cancellationToken)
    {
        using var boundCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        boundCts.CancelAfter(bound);

        try
        {
            await proc.WaitForExitAsync(boundCts.Token).ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException)
        {
            await ReclaimAsync(proc).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            return false;
        }
    }

    /// <summary>
    /// Kills and reaps the child. Nothing in here may escape: on the V1 rollback path the only enclosing
    /// handler is <c>catch (WebSocketException)</c> in <c>AiSpeechAssistantService.SendToTwilioAsync</c>,
    /// so any other exception type would leave the caller's microphone muted for the rest of the call and
    /// unwind the whole OpenAI receive loop. <c>Kill</c> throws on a disposed or unstarted Process
    /// (measured: InvalidOperationException), and KillTree can raise Win32Exception/AggregateException.
    /// </summary>
    private static async Task ReclaimAsync(Process proc)
    {
        try
        {
            proc.Kill(entireProcessTree: true);

            using var reapCts = new CancellationTokenSource(ReapBound);

            await proc.WaitForExitAsync(reapCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[FfmpegBound] Child could not be reclaimed after its bound");
        }
    }
}
