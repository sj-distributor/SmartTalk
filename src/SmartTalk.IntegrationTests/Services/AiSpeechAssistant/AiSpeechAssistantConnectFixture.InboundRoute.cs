using Shouldly;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistant;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantConnectFixture
{
    [Fact]
    public void ShouldReturnForwardNumber_WhenFullDayRouteMatches()
    {
        var now = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero);

        Run<AiSpeechAssistantService>(service =>
        {
            var routes = new List<AiSpeechAssistantInboundRoute>
            {
                new()
                {
                    ForwardNumber = "+15551112222", IsFullDay = true,
                    DayOfWeek = ((int)now.DayOfWeek).ToString(),
                    TimeZone = "UTC", Priority = 1
                }
            };

            var (forwardNumber, forwardAssistantId) = service.DecideDestinationByInboundRoute(routes);

            forwardNumber.ShouldBe("+15551112222");
            forwardAssistantId.ShouldBeNull();
        }, builder => MockClock(builder, now));
    }

    [Fact]
    public void ShouldReturnForwardAssistantId_WhenRouteMatches()
    {
        var now = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero);

        Run<AiSpeechAssistantService>(service =>
        {
            var routes = new List<AiSpeechAssistantInboundRoute>
            {
                new()
                {
                    ForwardAssistantId = 42, IsFullDay = true,
                    DayOfWeek = ((int)now.DayOfWeek).ToString(),
                    TimeZone = "UTC", Priority = 1
                }
            };

            var (forwardNumber, forwardAssistantId) = service.DecideDestinationByInboundRoute(routes);

            forwardNumber.ShouldBeNull();
            forwardAssistantId.ShouldBe(42);
        }, builder => MockClock(builder, now));
    }

    [Fact]
    public void ShouldReturnEmpty_WhenNoRoutes()
    {
        Run<AiSpeechAssistantService>(service =>
        {
            var (forwardNumber, forwardAssistantId) = service.DecideDestinationByInboundRoute(new List<AiSpeechAssistantInboundRoute>());

            forwardNumber.ShouldBeNull();
            forwardAssistantId.ShouldBeNull();
        }, MockExternalServices);
    }

    [Fact]
    public void ShouldPrioritizeEmergencyRoutes_OverNormal()
    {
        var now = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero);

        Run<AiSpeechAssistantService>(service =>
        {
            var routes = new List<AiSpeechAssistantInboundRoute>
            {
                new()
                {
                    ForwardNumber = "+15551111111", IsFullDay = true,
                    DayOfWeek = ((int)now.DayOfWeek).ToString(),
                    TimeZone = "UTC", Priority = 1, Emergency = false
                },
                new()
                {
                    ForwardNumber = "+15552222222", IsFullDay = true,
                    DayOfWeek = ((int)now.DayOfWeek).ToString(),
                    TimeZone = "UTC", Priority = 1, Emergency = true
                }
            };

            var (forwardNumber, forwardAssistantId) = service.DecideDestinationByInboundRoute(routes);

            forwardNumber.ShouldBe("+15552222222");
            forwardAssistantId.ShouldBeNull();
        }, builder => MockClock(builder, now));
    }

    [Fact]
    public void ShouldReturnEmpty_WhenDayDoesNotMatch()
    {
        var now = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero);
        var wrongDay = ((int)now.DayOfWeek + 1) % 7;

        Run<AiSpeechAssistantService>(service =>
        {
            var routes = new List<AiSpeechAssistantInboundRoute>
            {
                new()
                {
                    ForwardNumber = "+15551112222", IsFullDay = true,
                    DayOfWeek = wrongDay.ToString(),
                    TimeZone = "UTC", Priority = 1
                }
            };

            var (forwardNumber, forwardAssistantId) = service.DecideDestinationByInboundRoute(routes);

            forwardNumber.ShouldBeNull();
            forwardAssistantId.ShouldBeNull();
        }, builder => MockClock(builder, now));
    }

    [Fact]
    public void ShouldReturnEmpty_WhenTimeWindowDoesNotMatch()
    {
        var now = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero);

        Run<AiSpeechAssistantService>(service =>
        {
            var routes = new List<AiSpeechAssistantInboundRoute>
            {
                new()
                {
                    ForwardNumber = "+15551112222", IsFullDay = false,
                    DayOfWeek = ((int)now.DayOfWeek).ToString(),
                    StartTime = new TimeSpan(2, 0, 0),
                    EndTime = new TimeSpan(3, 0, 0),
                    TimeZone = "UTC", Priority = 1
                }
            };

            var (forwardNumber, forwardAssistantId) = service.DecideDestinationByInboundRoute(routes);

            forwardNumber.ShouldBeNull();
            forwardAssistantId.ShouldBeNull();
        }, builder => MockClock(builder, now));
    }
}
