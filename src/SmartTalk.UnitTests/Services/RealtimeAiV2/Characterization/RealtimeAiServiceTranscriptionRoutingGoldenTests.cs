using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the engine's transcription routing parity: of the four transcription
/// kinds (partial/completed × input/output), ONLY the two completed kinds are enqueued for end-of-session
/// persistence (OnTranscriptionsCompletedAsync), while ALL FOUR are sent to the client for live display.
/// Migration step S3 reroutes these through the Kind union; which events persist vs only display must be
/// preserved, or this fails RED.
/// </summary>
public class RealtimeAiServiceTranscriptionRoutingGoldenTests : RealtimeAiServiceTestBase
{
    private static ParsedRealtimeAiProviderEvent Transcript(RealtimeAiWssEventType type, string text, AiSpeechAssistantSpeaker speaker) =>
        new() { Type = type, Data = new RealtimeAiWssTranscriptionData { Transcript = text, Speaker = speaker } };

    [Fact]
    public async Task FourTranscriptionKinds_OnlyCompletedPersist_AllFourDisplay()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0) switch
        {
            "ip" => Transcript(RealtimeAiWssEventType.InputAudioTranscriptionPartial, "ip", AiSpeechAssistantSpeaker.User),
            "ic" => Transcript(RealtimeAiWssEventType.InputAudioTranscriptionCompleted, "ic", AiSpeechAssistantSpeaker.User),
            "op" => Transcript(RealtimeAiWssEventType.OutputAudioTranscriptionPartial, "op", AiSpeechAssistantSpeaker.Ai),
            _ => Transcript(RealtimeAiWssEventType.OutputAudioTranscriptionCompleted, "oc", AiSpeechAssistantSpeaker.Ai)
        });

        List<(AiSpeechAssistantSpeaker Speaker, string Text)>? persisted = null;
        var options = CreateDefaultOptions(o => o.OnTranscriptionsCompletedAsync = (_, t) => { persisted = t.ToList(); return Task.CompletedTask; });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("ip");
        await FakeWssClient.SimulateMessageReceivedAsync("ic");
        await FakeWssClient.SimulateMessageReceivedAsync("op");
        await FakeWssClient.SimulateMessageReceivedAsync("oc");

        FakeWs.EnqueueClose();
        await sessionTask;

        // Persistence: only the two COMPLETED transcripts, in arrival order.
        persisted.ShouldNotBeNull();
        persisted.ShouldBe(new[]
        {
            (AiSpeechAssistantSpeaker.User, "ic"),
            (AiSpeechAssistantSpeaker.Ai, "oc")
        });

        // Live display: all FOUR kinds call BuildTranscriptionMessage with their own event type.
        ClientAdapter.Received().BuildTranscriptionMessage(RealtimeAiWssEventType.InputAudioTranscriptionPartial, Arg.Any<RealtimeAiWssTranscriptionData>(), Arg.Any<string>());
        ClientAdapter.Received().BuildTranscriptionMessage(RealtimeAiWssEventType.InputAudioTranscriptionCompleted, Arg.Any<RealtimeAiWssTranscriptionData>(), Arg.Any<string>());
        ClientAdapter.Received().BuildTranscriptionMessage(RealtimeAiWssEventType.OutputAudioTranscriptionPartial, Arg.Any<RealtimeAiWssTranscriptionData>(), Arg.Any<string>());
        ClientAdapter.Received().BuildTranscriptionMessage(RealtimeAiWssEventType.OutputAudioTranscriptionCompleted, Arg.Any<RealtimeAiWssTranscriptionData>(), Arg.Any<string>());
    }
}
