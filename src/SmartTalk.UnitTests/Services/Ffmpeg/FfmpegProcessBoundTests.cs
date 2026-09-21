using System.Diagnostics;
using Shouldly;
using SmartTalk.Core.Services.Ffmpeg;
using Xunit;

namespace SmartTalk.UnitTests.Services.Ffmpeg;

/// <summary>
/// Tier: high-fidelity (Rule 12) — drives the real <see cref="FfmpegProcessBound"/> against a real OS
/// child process spawned in the same shape as <c>FfmpegService.ConvertWavToULawAsync</c> (redirected
/// stdout/stderr + BeginXxxReadLine), so the pipe-EOF behaviour of WaitForExitAsync is exercised for
/// real. ffmpeg itself is not required: the contract under test is "bound fires, child dies, caller
/// learns about it", which is orthogonal to what the child binary is.
///
/// <para>Unix-only (Rule 12.1). The gate runs on macOS and Linux; Windows returns early.</para>
/// </summary>
public class FfmpegProcessBoundTests
{
    // Guards against a regression that removes the bound entirely — without this the test would hang
    // the suite instead of failing it (Rule 12.10).
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(20);

    // ── Env var literal pinning (Rule 8) ────────────────────────

    [Fact]
    public void UlawBoundSecondsEnvVar_ConstantNamePinned()
    {
        FfmpegProcessBound.UlawBoundSecondsEnvVar
            .ShouldBe("SQUID_SMARTTALK_FFMPEG_ULAW_BOUND_SECONDS");
    }

    // ── ParseSeconds: default must survive every malformed / out-of-range value ──

    [Theory]
    [InlineData(null, 30)]
    [InlineData("", 30)]
    [InlineData("abc", 30)]
    [InlineData("0", 30)]
    [InlineData("-1", 30)]
    [InlineData("4", 30)]
    [InlineData("301", 30)]
    [InlineData("5", 5)]
    [InlineData("45", 45)]
    [InlineData("300", 300)]
    public void ParseSeconds_ShouldClampToRangeOrFallBackToDefault(string raw, int expected)
    {
        FfmpegProcessBound.ParseSeconds(raw).ShouldBe(expected);
    }

    [Fact]
    public void ResolveUlawBound_WithNoEnvVarSet_ShouldBeThirtySeconds()
    {
        // 30s must stay comfortably under the 60s idle-follow-up default in
        // AiSpeechAssistantConnectService.Build.Session.cs, so recovery and the microphone resume
        // land before any idle-driven hangup could compound the failure.
        FfmpegProcessBound.ResolveUlawBound().ShouldBe(TimeSpan.FromSeconds(30));
    }

    // ── The bound itself ────────────────────────────────────────

    [Fact]
    public async Task TryWaitForExitWithinAsync_ChildOutlivesBound_ShouldReturnFalseAndKillIt()
    {
        if (!IsUnix()) return;

        using var proc = StartChild("30");
        var pid = proc.Id;

        var result = await WithinHangGuard(FfmpegProcessBound.TryWaitForExitWithinAsync(proc, TimeSpan.FromSeconds(1), CancellationToken.None));

        result.ShouldBeFalse("the bound must report the timeout so ConvertWavToULawAsync returns empty instead of holding the provider receive loop forever");
        proc.HasExited.ShouldBeTrue("the child must be dead before the caller's finally unlinks the temp files - unlinking does not stop ffmpeg, it keeps its fds on the deleted inodes");
        IsAlive(pid).ShouldBeFalse($"pid {pid} outlived the bound; if this fails on CI, check for leaked children with `ps -p {pid}` / `pgrep -f sleep`");
    }

    [Fact]
    public async Task TryWaitForExitWithinAsync_SlowButHealthyChild_ShouldReturnTrueAndLeaveItUntouched()
    {
        if (!IsUnix()) return;

        // Deliberately slow relative to the bound rather than instant: an instant child would still
        // pass even if the bound fired at the wrong scale, so it would not guard the one regression
        // that costs call quality - a bound that fires on a conversion that was going to succeed.
        using var proc = StartChild("1.5");

        var result = await WithinHangGuard(FfmpegProcessBound.TryWaitForExitWithinAsync(proc, TimeSpan.FromSeconds(5), CancellationToken.None));

        result.ShouldBeTrue("a healthy conversion must be reported as a clean exit, not a timeout - reporting a timeout here silently drops the repeat-order readback on a live call");
        proc.ExitCode.ShouldBe(0, "a healthy conversion must not be killed - exit code 137 means the bound fired on a good run");
    }

    [Fact]
    public async Task TryWaitForExitWithinAsync_CallerCancels_ShouldRethrowAndStillKillTheChild()
    {
        if (!IsUnix()) return;

        using var cts = new CancellationTokenSource();
        using var proc = StartChild("30");
        var pid = proc.Id;

        cts.CancelAfter(TimeSpan.FromMilliseconds(300));

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await WithinHangGuard(FfmpegProcessBound.TryWaitForExitWithinAsync(proc, TimeSpan.FromMinutes(5), cts.Token)));

        IsAlive(pid).ShouldBeFalse($"caller cancellation must not leak the child; pid {pid} is still running");
    }

    [Fact]
    public async Task TryWaitForExitWithinAsync_ChildLeavesADescendantHoldingThePipes_ShouldStillReturn()
    {
        if (!IsUnix()) return;

        // The direct child exits immediately but its grandchild inherits the redirected pipes, so
        // WaitForExitAsync never sees EOF. Real ffmpeg spawns no children, but the reap must not be
        // able to reintroduce the unbounded wait this whole change exists to remove.
        using var proc = StartShell("sleep 25 & exit 0");

        var result = await WithinHangGuard(FfmpegProcessBound.TryWaitForExitWithinAsync(proc, TimeSpan.FromSeconds(1), CancellationToken.None));

        result.ShouldBeFalse("a pipe-EOF stall must be reported as a timeout, not hang the receive loop");

        Cleanup("sleep 25");
    }

    // ── helpers ─────────────────────────────────────────────────

    private static bool IsUnix() => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    private static Process StartChild(string seconds) => Start("/bin/sleep", seconds);

    private static Process StartShell(string script) => Start("/bin/sh", $"-c \"{script}\"");

    /// <summary>Mirrors the process shape of FfmpegService.ConvertWavToULawAsync exactly.</summary>
    private static Process Start(string fileName, string arguments)
    {
        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        proc.ErrorDataReceived += (_, _) => { };
        proc.OutputDataReceived += (_, _) => { };

        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        return proc;
    }

    private static async Task<bool> WithinHangGuard(Task<bool> call)
    {
        if (await Task.WhenAny(call, Task.Delay(HangGuard)).ConfigureAwait(false) != call)
            throw new TimeoutException($"TryWaitForExitWithinAsync did not return within {HangGuard.TotalSeconds}s - the bound never fired. This is the exact production failure: the provider receive loop is held forever and the caller stays muted.");

        return await call.ConfigureAwait(false);
    }

    private static bool IsAlive(int pid)
    {
        try
        {
            return !Process.GetProcessById(pid).HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void Cleanup(string pattern)
    {
        try
        {
            using var k = Process.Start("/bin/sh", $"-c \"pkill -f '{pattern}' || true\"");
            k.WaitForExit(2000);
        }
        catch
        {
            /* best effort */
        }
    }
}
