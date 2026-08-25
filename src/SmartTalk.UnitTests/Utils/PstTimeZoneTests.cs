using Shouldly;
using SmartTalk.Core.Utils;
using Xunit;

namespace SmartTalk.UnitTests.Utils;

/// <summary>
/// Verifies <see cref="PstTimeZone"/> returns the Pacific Standard Time zone
/// regardless of host OS (Windows ID vs IANA ID).
///
/// <para>
/// .NET 6+ already converts between Windows and IANA IDs internally, and .NET 8
/// caches <see cref="TimeZoneInfo"/> instances by ID. This helper exists to
/// (a) centralize the magic string, (b) provide explicit cross-platform
/// fallback as defense-in-depth for environments without ICU/tzdata data, and
/// (c) eager-fail with a clear actionable message instead of a stack trace.
/// </para>
/// </summary>
public class PstTimeZoneTests
{
    [Fact]
    public void Get_ReturnsNonNullTimeZone()
    {
        PstTimeZone.Get().ShouldNotBeNull();
    }

    [Fact]
    public void Get_ReturnsTimeZoneWithBaseUtcOffsetMinusEightHours()
    {
        // PST baseline (winter) is UTC-8. Daylight savings doesn't change BaseUtcOffset,
        // only the active offset (which depends on date). This assertion is
        // platform-independent (Windows ID and IANA ID both resolve to the same zone).
        PstTimeZone.Get().BaseUtcOffset.ShouldBe(TimeSpan.FromHours(-8));
    }

    [Fact]
    public void Get_CalledTwice_ReturnsSameInstance()
    {
        var first = PstTimeZone.Get();
        var second = PstTimeZone.Get();

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    [Fact]
    public void Get_ReturnsZoneWithRecognizableId()
    {
        // Either the Windows-style or IANA-style ID is acceptable depending on the
        // platform. Just verifies we resolved a real PST/PDT zone.
        var id = PstTimeZone.Get().Id;

        id.ShouldBeOneOf("Pacific Standard Time", "America/Los_Angeles");
    }
}
