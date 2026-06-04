using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Recording;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins the env var literals and the default-preserving fallback semantics
/// of <see cref="RealtimeAiRecordingSettings"/>.
///
/// <para>Default mode is Unbounded — i.e. zero behaviour change vs. pre-Phase-3
/// production. Operators must explicitly set BufferMode=rolling to enable
/// the new bounded buffer.</para>
/// </summary>
public class RealtimeAiRecordingSettingsTests
{
    // ── Env var literal pinning (Rule 8) ────────────────────────

    [Fact]
    public void BufferModeEnvVar_ConstantNamePinned()
    {
        RealtimeAiRecordingSettings.BufferModeEnvVar
            .ShouldBe("SQUID_SMARTTALK_RECORDING_BUFFER_MODE");
    }

    [Fact]
    public void BufferSecondsEnvVar_ConstantNamePinned()
    {
        RealtimeAiRecordingSettings.BufferSecondsEnvVar
            .ShouldBe("SQUID_SMARTTALK_RECORDING_BUFFER_SECONDS");
    }

    // ── ParseMode ───────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unbounded")]
    [InlineData("Unbounded")]
    [InlineData("UNBOUNDED")]
    [InlineData("anything-else")]
    [InlineData("garbage")]
    public void ParseMode_NotRolling_ReturnsUnbounded(string raw)
    {
        RealtimeAiRecordingSettings.ParseMode(raw)
            .ShouldBe(RealtimeAiRecordingSettings.BufferMode.Unbounded);
    }

    [Theory]
    [InlineData("rolling")]
    [InlineData("Rolling")]
    [InlineData("ROLLING")]
    [InlineData("  rolling  ")]
    public void ParseMode_RollingValueAnyCase_ReturnsRolling(string raw)
    {
        RealtimeAiRecordingSettings.ParseMode(raw)
            .ShouldBe(RealtimeAiRecordingSettings.BufferMode.Rolling);
    }

    // ── ParseSeconds ────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("29")]                      // below min
    [InlineData("3601")]                    // above max
    [InlineData("999999")]
    public void ParseSeconds_InvalidOrOutOfRange_ReturnsDefault300(string raw)
    {
        RealtimeAiRecordingSettings.ParseSeconds(raw).ShouldBe(300);
    }

    [Theory]
    [InlineData("30", 30)]                   // min boundary
    [InlineData("60", 60)]
    [InlineData("300", 300)]                 // default value via env
    [InlineData("600", 600)]
    [InlineData("3600", 3600)]               // max boundary
    public void ParseSeconds_ValidValueInRange_ReturnsThatValue(string raw, int expected)
    {
        RealtimeAiRecordingSettings.ParseSeconds(raw).ShouldBe(expected);
    }

    // ── Create() integration ────────────────────────────────────

    [Fact]
    public async Task Create_DefaultEnv_ReturnsUnboundedBuffer()
    {
        // Defensive: clear the env var in case of leftover from another test run.
        Environment.SetEnvironmentVariable(RealtimeAiRecordingSettings.BufferModeEnvVar, null);
        Environment.SetEnvironmentVariable(RealtimeAiRecordingSettings.BufferSecondsEnvVar, null);

        await using var buffer = RealtimeAiRecordingSettings.Create();

        buffer.ShouldBeOfType<UnboundedMemoryBuffer>();
    }

    [Fact]
    public async Task Create_RollingMode_ReturnsRollingWindowBuffer()
    {
        Environment.SetEnvironmentVariable(RealtimeAiRecordingSettings.BufferModeEnvVar, "rolling");
        Environment.SetEnvironmentVariable(RealtimeAiRecordingSettings.BufferSecondsEnvVar, "60");
        try
        {
            await using var buffer = RealtimeAiRecordingSettings.Create();

            buffer.ShouldBeOfType<RollingWindowBuffer>();
        }
        finally
        {
            Environment.SetEnvironmentVariable(RealtimeAiRecordingSettings.BufferModeEnvVar, null);
            Environment.SetEnvironmentVariable(RealtimeAiRecordingSettings.BufferSecondsEnvVar, null);
        }
    }
}
