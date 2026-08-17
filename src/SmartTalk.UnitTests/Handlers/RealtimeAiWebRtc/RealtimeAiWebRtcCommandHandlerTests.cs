using Mediator.Net.Context;
using NSubstitute;
using SmartTalk.Core.Handlers.CommandHandlers.RealtimeAiWebRtc;
using SmartTalk.Core.Services.RealtimeAiWebRtc;
using SmartTalk.Messages.Commands.RealtimeAiWebRtc;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Handlers.RealtimeAiWebRtc;

public class RealtimeAiWebRtcCommandHandlerTests
{
    [Fact]
    public async Task CreateSession_ForwardsCommandAndMapsResult()
    {
        var registry = Substitute.For<IRealtimeAiWebRtcSessionRegistry>();
        var command = new CreateRealtimeAiWebRtcSessionCommand
        {
            AssistantId = 625,
            Region = RealtimeAiServerRegion.HK,
            OfferSdp = "offer"
        };
        var context = Substitute.For<IReceiveContext<CreateRealtimeAiWebRtcSessionCommand>>();
        context.Message.Returns(command);
        registry.CreateAsync(625, RealtimeAiServerRegion.HK, "offer", Arg.Any<CancellationToken>())
            .Returns(new RealtimeAiWebRtcCallResult
            {
                CallId = "rtc_test_123",
                AnswerSdp = "answer"
            });

        var response = await new CreateRealtimeAiWebRtcSessionCommandHandler(registry)
            .Handle(context, CancellationToken.None);

        Assert.Equal("rtc_test_123", response.CallId);
        Assert.Equal("answer", response.AnswerSdp);
    }

    [Fact]
    public async Task MarkClientReady_ReturnsRegistryResult()
    {
        var registry = Substitute.For<IRealtimeAiWebRtcSessionRegistry>();
        var context = Substitute.For<IReceiveContext<MarkRealtimeAiWebRtcClientReadyCommand>>();
        context.Message.Returns(new MarkRealtimeAiWebRtcClientReadyCommand { CallId = "rtc_test_123" });
        registry.MarkClientReadyAsync("rtc_test_123").Returns(true);

        var response = await new MarkRealtimeAiWebRtcClientReadyCommandHandler(registry)
            .Handle(context, CancellationToken.None);

        Assert.True(response.IsFound);
    }

    [Fact]
    public async Task StopSession_ReturnsRegistryResult()
    {
        var registry = Substitute.For<IRealtimeAiWebRtcSessionRegistry>();
        var context = Substitute.For<IReceiveContext<StopRealtimeAiWebRtcSessionCommand>>();
        context.Message.Returns(new StopRealtimeAiWebRtcSessionCommand { CallId = "rtc_test_123" });
        registry.StopAsync("rtc_test_123").Returns(false);

        var response = await new StopRealtimeAiWebRtcSessionCommandHandler(registry)
            .Handle(context, CancellationToken.None);

        Assert.False(response.IsFound);
    }
}
