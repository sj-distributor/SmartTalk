using Newtonsoft.Json;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.Agent;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

public class SelectTransferCallNumberTests
{
    private readonly AgentTransferCallRoutingService _routingService = new(Substitute.For<IPosDataProvider>());
    private static readonly DateTimeOffset MondayAtNoonPst =
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 2, 16, 12, 0, 0),
            TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));

    [Fact]
    public void SelectTransferCallNumber_DefaultHasHighestPriority()
    {
        var configs = new List<AgentTransferCallConfig>
        {
            new() { Id = 1, TransferCallNumber = "normal", Priority = AgentTransferCallPriority.Normal, ServiceHours = CreateMondayServiceHours(0, 23) },
            new() { Id = 2, TransferCallNumber = "default", Priority = AgentTransferCallPriority.Default, ServiceHours = CreateMondayServiceHours(0, 23) }
        };

        _routingService.SelectTransferCallNumber(configs, MondayAtNoonPst)
            .ShouldBe("default");
    }

    [Theory]
    [InlineData(10, "default")]
    [InlineData(14, "normal")]
    [InlineData(20, null)]
    public void SelectTransferCallNumber_SelectsNumberAvailableAtTransferTime(int hour, string expectedNumber)
    {
        var configs = new List<AgentTransferCallConfig>
        {
            new()
            {
                Id = 1,
                TransferCallNumber = "default",
                Priority = AgentTransferCallPriority.Default,
                ServiceHours = CreateMondayServiceHours(9, 12)
            },
            new()
            {
                Id = 2,
                TransferCallNumber = "normal",
                Priority = AgentTransferCallPriority.Normal,
                ServiceHours = CreateMondayServiceHours(12, 17)
            }
        };
        var transferTime = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 2, 16, hour, 0, 0),
            TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));

        _routingService.SelectTransferCallNumber(configs, transferTime)
            .ShouldBe(expectedNumber);
    }

    [Fact]
    public void SelectDefaultTransferCallNumber_IgnoresServiceHoursAndNormalNumbers()
    {
        var unavailableHours = JsonConvert.SerializeObject(new List<AgentServiceHoursDto>
        {
            new() { Day = (int)DayOfWeek.Monday, Hours = [] }
        });
        var configs = new List<AgentTransferCallConfig>
        {
            new() { Id = 1, TransferCallNumber = "normal", Priority = AgentTransferCallPriority.Normal, ServiceHours = unavailableHours },
            new()
            {
                Id = 2,
                TransferCallNumber = "default",
                Priority = AgentTransferCallPriority.Default,
                ServiceHours = unavailableHours
            }
        };

        _routingService.SelectDefaultTransferCallNumber(configs)
            .ShouldBe("default");
    }

    [Fact]
    public void SelectTransferCallNumber_SupportsApiServiceHoursFormatAndFiltersUnavailableNumbers()
    {
        var configs = new List<AgentTransferCallConfig>
        {
            new()
            {
                Id = 1,
                TransferCallNumber = "default-morning",
                Priority = AgentTransferCallPriority.Default,
                ServiceHours = """[{"day":1,"hours":[{"end":"09:00","start":"00:00"}]}]"""
            },
            new()
            {
                Id = 2,
                TransferCallNumber = "normal-daytime",
                Priority = AgentTransferCallPriority.Normal,
                ServiceHours = """[{"day":1,"hours":[{"end":"17:00","start":"09:01"}]}]"""
            },
            new()
            {
                Id = 3,
                TransferCallNumber = "normal-evening",
                Priority = AgentTransferCallPriority.Normal,
                ServiceHours = """[{"day":1,"hours":[{"end":"23:59","start":"17:01"}]}]"""
            }
        };

        _routingService.SelectTransferCallNumber(configs, MondayAtNoonPst)
            .ShouldBe("normal-daytime");
    }

    [Fact]
    public void SelectTransferCallNumber_UsesLowestIdWhenDifferentNormalServiceHoursOverlap()
    {
        const string mondayToWednesdayHours = """
            [{"day":1,"hours":[{"end":"17:00","start":"09:00"}]},{"day":2,"hours":[{"end":"17:00","start":"09:00"}]},{"day":3,"hours":[{"end":"17:00","start":"09:00"}]}]
            """;
        const string wednesdayToFridayHours = """
            [{"day":3,"hours":[{"end":"15:00","start":"11:00"}]},{"day":4,"hours":[{"end":"15:00","start":"11:00"}]},{"day":5,"hours":[{"end":"15:00","start":"11:00"}]}]
            """;
        var configs = new List<AgentTransferCallConfig>
        {
            new()
            {
                Id = 3,
                TransferCallNumber = "normal-later",
                Priority = AgentTransferCallPriority.Normal,
                ServiceHours = mondayToWednesdayHours
            },
            new()
            {
                Id = 1,
                TransferCallNumber = "default-unavailable",
                Priority = AgentTransferCallPriority.Default,
                ServiceHours = """[{"day":3,"hours":[{"end":"08:59","start":"00:00"}]}]"""
            },
            new()
            {
                Id = 2,
                TransferCallNumber = "normal-earlier",
                Priority = AgentTransferCallPriority.Normal,
                ServiceHours = wednesdayToFridayHours
            }
        };
        var wednesdayAtNoonPst = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 2, 18, 12, 0, 0),
            TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));

        _routingService.SelectTransferCallNumber(configs, wednesdayAtNoonPst)
            .ShouldBe("normal-earlier");
    }

    [Fact]
    public void SelectTransferCallNumber_UsesAgentTimeZone()
    {
        var configs = new List<AgentTransferCallConfig>
        {
            new()
            {
                Id = 1,
                TransferCallNumber = "morning",
                Priority = AgentTransferCallPriority.Default,
                ServiceHours = CreateMondayServiceHours(8, 10)
            },
            new()
            {
                Id = 2,
                TransferCallNumber = "afternoon",
                Priority = AgentTransferCallPriority.Normal,
                ServiceHours = CreateMondayServiceHours(11, 13)
            }
        };
        var utcNow = new DateTimeOffset(2026, 2, 16, 17, 0, 0, TimeSpan.Zero);
        var losAngeles = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        _routingService.SelectTransferCallNumber(configs, utcNow, losAngeles)
            .ShouldBe("morning");
        _routingService.SelectTransferCallNumber(configs, utcNow, newYork)
            .ShouldBe("afternoon");
    }

    [Fact]
    public async Task ResolveTimeZoneAsync_UsesLosAngelesWhenTimeZoneIsMissingOrInvalid()
    {
        var missingTimeZone = await _routingService.ResolveTimeZoneAsync(new Agent());
        var invalidTimeZone = await _routingService.ResolveTimeZoneAsync(new Agent { Timezone = "invalid/timezone" });

        missingTimeZone
            .ShouldBe(PstTimeZone.Get());
        invalidTimeZone
            .ShouldBe(PstTimeZone.Get());
    }

    [Fact]
    public async Task ResolveTimeZoneAsync_UsesStoreTimeZoneWhenAgentTimeZoneIsMissing()
    {
        var posDataProvider = Substitute.For<IPosDataProvider>();
        posDataProvider.GetPosStoreByAgentIdAsync(42, Arg.Any<CancellationToken>())
            .Returns(new CompanyStore { Timezone = "America/New_York" });
        var routingService = new AgentTransferCallRoutingService(posDataProvider);

        var timeZone = await routingService.ResolveTimeZoneAsync(new Agent { Id = 42 });

        timeZone.ShouldBe(TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
    }

    private static string CreateMondayServiceHours(int startHour, int endHour)
    {
        return $"[{{\"day\":1,\"hours\":[{{\"end\":\"{endHour:00}:00\",\"start\":\"{startHour:00}:00\"}}]}}]";
    }
}
