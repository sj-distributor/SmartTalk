using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Wss;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins the `KeepAliveSecondsEnvVar` literal and verifies the parse contract for the
/// shared keep-alive setting used by both OpenAI and Google realtime wss clients.
///
/// <para>
/// The default (15s) is tighter than .NET's 30s baseline because OpenAI Realtime sends
/// no traffic during AI-silent windows; corporate proxies and cloud LBs that idle-out
/// long-lived TLS at ~30s have been observed to drop the connection mid-call. Caller
/// hears silence until the next utterance triggers a reconnect attempt.
/// </para>
///
/// <para>
/// Air-gapped / fork operators can override via env var (Rule 8). The test pinning the
/// env var literal makes a future rename a compile-time-visible decision rather than a
/// silent breakage of pinned production overrides.
/// </para>
/// </summary>
public class RealtimeAiWebSocketSettingsTests
{
    [Fact]
    public void KeepAliveSecondsEnvVar_ConstantNamePinned()
    {
        // Renaming this constant breaks every operator who pinned a custom keep-alive
        // via env var. Hard-pin in test.
        RealtimeAiWebSocketSettings.KeepAliveSecondsEnvVar
            .ShouldBe("SQUID_SMARTTALK_REALTIME_WS_KEEPALIVE_SECONDS");
    }

    [Theory]
    [InlineData(null)]                                  // unset env
    [InlineData("")]                                    // empty
    [InlineData("   ")]                                 // whitespace
    [InlineData("abc")]                                 // non-numeric
    [InlineData("15.5")]                                // decimal (we require int)
    [InlineData("-10")]                                 // negative
    [InlineData("0")]                                   // below min
    [InlineData("4")]                                   // below min
    [InlineData("121")]                                 // above max
    [InlineData("9999")]                                // way above max
    public void Parse_InvalidOrOutOfRangeValue_ReturnsDefaultFifteenSeconds(string raw)
    {
        RealtimeAiWebSocketSettings.Parse(raw).ShouldBe(TimeSpan.FromSeconds(15));
    }

    [Theory]
    [InlineData("5", 5)]                                // min boundary
    [InlineData("15", 15)]                              // default value via env
    [InlineData("30", 30)]                              // .NET default
    [InlineData("60", 60)]
    [InlineData("120", 120)]                            // max boundary
    public void Parse_ValidValueInRange_ReturnsThatValue(string raw, int expectedSeconds)
    {
        RealtimeAiWebSocketSettings.Parse(raw).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }
}
