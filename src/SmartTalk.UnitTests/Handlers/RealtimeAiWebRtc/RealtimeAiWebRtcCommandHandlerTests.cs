using Mediator.Net.Context;
using NSubstitute;
using SmartTalk.Core.Handlers.CommandHandlers.RealtimeAiWebRtc;
using SmartTalk.Core.Services.AiSpeechAssistant;
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
        var credentialService = Substitute.For<IAiSpeechAssistantSessionCredentialService>();
        var sessionId = Guid.Parse("753a2495-e52f-498c-8cd2-900de42f90ce");
        var command = new CreateRealtimeAiWebRtcSessionCommand
        {
            SessionId = sessionId,
            AssistantId = 625,
            Region = RealtimeAiServerRegion.HK,
            OfferSdp = "offer"
        };
        var context = Substitute.For<IReceiveContext<CreateRealtimeAiWebRtcSessionCommand>>();
        context.Message.Returns(command);
        credentialService.ReserveWebRtcAsync(
                sessionId,
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(AiSpeechAssistantSessionWebRtcTransitionStatus.Succeeded);
        credentialService.ActivateWebRtcAsync(
                sessionId,
                Arg.Any<string>(),
                "rtc_test_123",
                Arg.Any<CancellationToken>())
            .Returns(AiSpeechAssistantSessionWebRtcTransitionStatus.Succeeded);
        registry.CreateAsync(625, RealtimeAiServerRegion.HK, "offer", Arg.Any<CancellationToken>())
            .Returns(new RealtimeAiWebRtcCallResult
            {
                CallId = "rtc_test_123",
                AnswerSdp = "answer"
            });

        var response = await new CreateRealtimeAiWebRtcSessionCommandHandler(registry, credentialService)
            .Handle(context, CancellationToken.None);

        Assert.Equal("rtc_test_123", response.CallId);
        Assert.Equal("answer", response.AnswerSdp);
    }

    [Fact]
    public async Task CreateSession_WhenCredentialIsAlreadyBound_DoesNotCreateAnotherCall()
    {
        var registry = Substitute.For<IRealtimeAiWebRtcSessionRegistry>();
        var credentialService = Substitute.For<IAiSpeechAssistantSessionCredentialService>();
        var sessionId = Guid.Parse("753a2495-e52f-498c-8cd2-900de42f90ce");
        var context = Substitute.For<IReceiveContext<CreateRealtimeAiWebRtcSessionCommand>>();
        context.Message.Returns(new CreateRealtimeAiWebRtcSessionCommand
        {
            SessionId = sessionId,
            AssistantId = 625,
            Region = RealtimeAiServerRegion.HK,
            OfferSdp = "offer"
        });
        credentialService.ReserveWebRtcAsync(
                sessionId,
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(AiSpeechAssistantSessionWebRtcTransitionStatus.Conflict);

        var response = await new CreateRealtimeAiWebRtcSessionCommandHandler(registry, credentialService)
            .Handle(context, CancellationToken.None);

        Assert.True(response.IsSessionAlreadyBound);
        await registry.DidNotReceiveWithAnyArgs()
            .CreateAsync(default, default, default, default);
    }

    [Fact]
    public async Task CreateSession_WhenInitializationFails_ReleasesReservation()
    {
        var registry = Substitute.For<IRealtimeAiWebRtcSessionRegistry>();
        var credentialService = Substitute.For<IAiSpeechAssistantSessionCredentialService>();
        var sessionId = Guid.Parse("753a2495-e52f-498c-8cd2-900de42f90ce");
        var context = Substitute.For<IReceiveContext<CreateRealtimeAiWebRtcSessionCommand>>();
        context.Message.Returns(new CreateRealtimeAiWebRtcSessionCommand
        {
            SessionId = sessionId,
            AssistantId = 625,
            Region = RealtimeAiServerRegion.HK,
            OfferSdp = "offer"
        });
        credentialService.ReserveWebRtcAsync(
                sessionId,
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(AiSpeechAssistantSessionWebRtcTransitionStatus.Succeeded);
        registry.CreateAsync(
                625,
                RealtimeAiServerRegion.HK,
                "offer",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<RealtimeAiWebRtcCallResult>(
                new InvalidOperationException("initialization failed")));

        var handler = new CreateRealtimeAiWebRtcSessionCommandHandler(registry, credentialService);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(context, CancellationToken.None));

        Assert.Equal("initialization failed", exception.Message);
        await credentialService.Received(1).ReleaseWebRtcReservationAsync(
            sessionId,
            Arg.Any<string>(),
            CancellationToken.None);
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

    [Fact]
    public async Task AppendRecording_ReturnsRegistryResult()
    {
        var registry = Substitute.For<IRealtimeAiWebRtcSessionRegistry>();
        var pcmBytes = new byte[] { 1, 0, 2, 0 };
        var context = Substitute.For<IReceiveContext<AppendRealtimeAiWebRtcRecordingCommand>>();
        context.Message.Returns(new AppendRealtimeAiWebRtcRecordingCommand
        {
            CallId = "rtc_test_123",
            Sequence = 3,
            IsFinal = true,
            PcmBytes = pcmBytes
        });
        registry.AppendRecordingAsync("rtc_test_123", 3, pcmBytes, true).Returns(
            new AppendRealtimeAiWebRtcRecordingResponse
            {
                Status = RealtimeAiWebRtcRecordingAppendStatus.Accepted,
                NextSequence = 4
            });

        var response = await new AppendRealtimeAiWebRtcRecordingCommandHandler(registry)
            .Handle(context, CancellationToken.None);

        Assert.Equal(RealtimeAiWebRtcRecordingAppendStatus.Accepted, response.Status);
        Assert.Equal(4, response.NextSequence);
    }
}
