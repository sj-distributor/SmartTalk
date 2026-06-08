using SmartTalk.Core.Services.Sale;
using Xunit;

namespace SmartTalk.UnitTests.Services.Sale;

public class SalesAutoCreateServiceTests
{
    [Fact]
    public void IsSamePacificDay_ReturnsTrueForSamePacificCalendarDate()
    {
        var morningUtc = new DateTimeOffset(2026, 3, 2, 16, 0, 0, TimeSpan.Zero);
        var eveningUtc = new DateTimeOffset(2026, 3, 3, 7, 59, 0, TimeSpan.Zero);

        Assert.True(SalesAutoCreateService.IsSamePacificDay(morningUtc, eveningUtc));
    }

    [Fact]
    public void IsSamePacificDay_ReturnsFalseForDifferentPacificCalendarDates()
    {
        var dayOneUtc = new DateTimeOffset(2026, 3, 2, 16, 0, 0, TimeSpan.Zero);
        var dayTwoUtc = new DateTimeOffset(2026, 3, 3, 9, 0, 0, TimeSpan.Zero);

        Assert.False(SalesAutoCreateService.IsSamePacificDay(dayOneUtc, dayTwoUtc));
    }

    [Fact]
    public void ShouldSkipAutomaticSyncForInitialReleaseDay_ReturnsTrueWhenSamePacificDay()
    {
        var latestInitialReleaseUtc = new DateTimeOffset(2026, 3, 2, 16, 0, 0, TimeSpan.Zero);
        var currentUtc = new DateTimeOffset(2026, 3, 3, 7, 59, 0, TimeSpan.Zero);

        Assert.True(SalesAutoCreateService.ShouldSkipAutomaticSyncForInitialReleaseDay(latestInitialReleaseUtc, currentUtc));
    }

    [Fact]
    public void ShouldSkipAutomaticSyncForInitialReleaseDay_ReturnsFalseWhenNoInitialReleaseExists()
    {
        var currentUtc = new DateTimeOffset(2026, 3, 3, 7, 59, 0, TimeSpan.Zero);

        Assert.False(SalesAutoCreateService.ShouldSkipAutomaticSyncForInitialReleaseDay(null, currentUtc));
    }
}
