using Newtonsoft.Json;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Dto.Agent;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

public class CheckIfInServiceHoursTests
{
    private readonly AgentTransferCallRoutingService _routingService = new(Substitute.For<IPosDataProvider>());
    private static readonly TimeZoneInfo PstZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

    /// <summary>
    /// Helper: build a UTC DateTimeOffset that corresponds to the given PST local time.
    /// </summary>
    private static DateTimeOffset PstToUtc(int year, int month, int day, int hour, int minute) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0), PstZone);

    private static string BuildServiceHours(params (int day, TimeSpan start, TimeSpan end)[] windows)
    {
        var hours = Enumerable.Range(0, 7).Select(d =>
        {
            var matched = windows.Where(w => w.day == d).ToList();
            return new AgentServiceHoursDto
            {
                Day = d,
                Hours = matched.Select(w => new HoursDto { Start = w.start, End = w.end }).ToList()
            };
        }).ToList();

        return JsonConvert.SerializeObject(hours);
    }

    [Fact]
    public void NullServiceHours_ReturnsInService()
    {
        var (inService, _) = _routingService.CheckIfInServiceHours(
            null, false, null, DateTimeOffset.UtcNow);

        inService.ShouldBeTrue();
    }

    [Fact]
    public void NullServiceHours_ManualServiceEnabled_WhenTransferConfigured()
    {
        var (_, manualService) = _routingService.CheckIfInServiceHours(
            null, true, "+15551234567", DateTimeOffset.UtcNow);

        manualService.ShouldBeTrue();
    }

    [Fact]
    public void NullServiceHours_ManualServiceDisabled_WhenNoTransferNumber()
    {
        var (_, manualService) = _routingService.CheckIfInServiceHours(
            null, true, null, DateTimeOffset.UtcNow);

        manualService.ShouldBeFalse();
    }

    [Fact]
    public void NullServiceHours_ManualServiceDisabled_WhenTransferHumanFalse()
    {
        var (_, manualService) = _routingService.CheckIfInServiceHours(
            null, false, "+15551234567", DateTimeOffset.UtcNow);

        manualService.ShouldBeFalse();
    }

    [Fact]
    public void WithinServiceWindow_ReturnsInService()
    {
        // Monday 09:00–17:00 PST, call at Monday 12:00 PST
        var json = BuildServiceHours((1, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0)));
        // 2026-02-16 is Monday
        var utcNow = PstToUtc(2026, 2, 16, 12, 0);

        var (inService, _) = _routingService.CheckIfInServiceHours(
            json, false, null, utcNow);

        inService.ShouldBeTrue();
    }

    [Fact]
    public void OutsideServiceWindow_ReturnsNotInService()
    {
        // Monday 09:00–17:00 PST, call at Monday 18:00 PST
        var json = BuildServiceHours((1, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0)));
        var utcNow = PstToUtc(2026, 2, 16, 18, 0);

        var (inService, _) = _routingService.CheckIfInServiceHours(
            json, false, null, utcNow);

        inService.ShouldBeFalse();
    }

    [Fact]
    public void AtExactStartTime_ReturnsInService()
    {
        var json = BuildServiceHours((1, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0)));
        var utcNow = PstToUtc(2026, 2, 16, 9, 0);

        var (inService, _) = _routingService.CheckIfInServiceHours(
            json, false, null, utcNow);

        inService.ShouldBeTrue();
    }

    [Fact]
    public void AtExactEndTime_ReturnsInService()
    {
        var json = BuildServiceHours((1, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0)));
        var utcNow = PstToUtc(2026, 2, 16, 17, 0);

        var (inService, _) = _routingService.CheckIfInServiceHours(
            json, false, null, utcNow);

        inService.ShouldBeTrue();
    }

    [Fact]
    public void WrongDayOfWeek_ReturnsNotInService()
    {
        // Only Tuesday (2) configured, but call on Monday (1)
        var json = BuildServiceHours((2, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0)));
        var utcNow = PstToUtc(2026, 2, 16, 12, 0); // Monday

        var (inService, _) = _routingService.CheckIfInServiceHours(
            json, false, null, utcNow);

        inService.ShouldBeFalse();
    }

    [Fact]
    public void MultipleWindowsSameDay_MatchesSecondWindow()
    {
        // Monday: 08:00–11:00 and 13:00–17:00, call at 14:00
        var json = BuildServiceHours(
            (1, new TimeSpan(8, 0, 0), new TimeSpan(11, 0, 0)),
            (1, new TimeSpan(13, 0, 0), new TimeSpan(17, 0, 0)));
        var utcNow = PstToUtc(2026, 2, 16, 14, 0);

        var (inService, _) = _routingService.CheckIfInServiceHours(
            json, false, null, utcNow);

        inService.ShouldBeTrue();
    }

    [Fact]
    public void MultipleWindowsSameDay_GapBetweenWindows_ReturnsNotInService()
    {
        // Monday: 08:00–11:00 and 13:00–17:00, call at 12:00 (in gap)
        var json = BuildServiceHours(
            (1, new TimeSpan(8, 0, 0), new TimeSpan(11, 0, 0)),
            (1, new TimeSpan(13, 0, 0), new TimeSpan(17, 0, 0)));
        var utcNow = PstToUtc(2026, 2, 16, 12, 0);

        var (inService, _) = _routingService.CheckIfInServiceHours(
            json, false, null, utcNow);

        inService.ShouldBeFalse();
    }

    [Fact]
    public void EmptyHoursForDay_ReturnsNotInService()
    {
        // All days configured but with empty hours lists
        var hours = Enumerable.Range(0, 7).Select(d => new AgentServiceHoursDto
        {
            Day = d, Hours = new List<HoursDto>()
        }).ToList();
        var json = JsonConvert.SerializeObject(hours);
        var utcNow = PstToUtc(2026, 2, 16, 12, 0);

        var (inService, _) = _routingService.CheckIfInServiceHours(
            json, false, null, utcNow);

        inService.ShouldBeFalse();
    }

    [Fact]
    public void SerializedNullServiceHours_ReturnsNotInService()
    {
        var (inService, _) = _routingService.CheckIfInServiceHours(
            "null", false, null, DateTimeOffset.UtcNow);

        inService.ShouldBeFalse();
    }

    [Fact]
    public void ManualServiceIndependentOfServiceHours()
    {
        // Outside service hours but manual service configured
        var json = BuildServiceHours((1, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0)));
        var utcNow = PstToUtc(2026, 2, 16, 18, 0);

        var (inService, manualService) = _routingService.CheckIfInServiceHours(
            json, true, "+15551234567", utcNow);

        inService.ShouldBeFalse();
        manualService.ShouldBeTrue();
    }

    [Fact]
    public void SecondsIgnored_OnlyHoursAndMinutesCompared()
    {
        // Window 09:00–09:01, call at 09:00:59 PST → still in service (truncated to 09:00)
        var json = BuildServiceHours((1, new TimeSpan(9, 0, 0), new TimeSpan(9, 1, 0)));
        var utc = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 2, 16, 9, 0, 59), PstZone);

        var (inService, _) = _routingService.CheckIfInServiceHours(
            json, false, null, new DateTimeOffset(utc, TimeSpan.Zero));

        inService.ShouldBeTrue();
    }

    [Fact]
    public void ServiceHours_UsesAgentTimeZone()
    {
        var json = BuildServiceHours((1, new TimeSpan(8, 0, 0), new TimeSpan(10, 0, 0)));
        var utcNow = new DateTimeOffset(2026, 2, 16, 17, 0, 0, TimeSpan.Zero);
        var losAngeles = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        _routingService.CheckIfInServiceHours(
                json, false, null, utcNow, losAngeles).IsInServiceHours
            .ShouldBeTrue();
        _routingService.CheckIfInServiceHours(
                json, false, null, utcNow, newYork).IsInServiceHours
            .ShouldBeFalse();
    }

    [Fact]
    public void MissingTimeZone_UsesPstByDefault()
    {
        var json = BuildServiceHours((1, new TimeSpan(8, 0, 0), new TimeSpan(10, 0, 0)));
        var utcNow = new DateTimeOffset(2026, 2, 16, 17, 0, 0, TimeSpan.Zero);

        var defaultResult = _routingService.CheckIfInServiceHours(
            json, false, null, utcNow);
        var explicitPstResult = _routingService.CheckIfInServiceHours(
            json, false, null, utcNow, PstZone);

        defaultResult.ShouldBe(explicitPstResult);
    }
}
