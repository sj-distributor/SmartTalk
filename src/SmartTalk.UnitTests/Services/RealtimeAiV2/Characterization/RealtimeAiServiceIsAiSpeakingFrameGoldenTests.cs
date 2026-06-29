using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the BuiltIn IsAiSpeaking lifecycle as TWO INDEPENDENT facts on a
/// single client frame: while the AI is speaking a client frame is (a) suppressed from the recording
/// but (b) still forwarded to the provider; once ResponseAudioDone flips IsAiSpeaking false, the next
/// frame IS recorded. The existing recording tests prove suppression only via fuzzy AGGREGATE byte
/// counts, so a refactor flipping IsAiSpeaking a frame early/late stays green. Here the exact recorded
/// WAV length pins it. Guards S5 (gating the early reset on OutputMode) and S6/S9–S12 (relocating where
/// IsAiSpeaking is set/reset).
/// </summary>
public class RealtimeAiServiceIsAiSpeakingFrameGoldenTests : RealtimeAiServiceTestBase
{
    private static string B64(byte fill, int count) => System.Convert.ToBase64String(Enumerable.Repeat(fill, count).ToArray());

    [Fact]
    public async Task BuiltIn_WhileSpeaking_SuppressesClientFrameFromRecording_ButStillForwardsToProvider()
    {
        var aiAudio = B64(0x07, 20);    // 20 PCM16 bytes of AI audio — recorded (provider source, always)
        var frame1 = B64(0x01, 12);     // 12 bytes, arrives while speaking → suppressed from recording
        var frame2 = B64(0x02, 12);     // 12 bytes, arrives after speaking → recorded

        ProviderAdapter.ParseMessage("ai-audio").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseAudioDelta,
            Data = new RealtimeAiWssAudioData { ItemId = "item", Base64Payload = aiAudio }
        });
        ProviderAdapter.ParseMessage("audio-done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseAudioDone
        });

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(ci => new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = ci.ArgAt<string>(0) == "f1" ? frame1 : frame2 });

        byte[]? wav = null;
        var options = CreateDefaultOptions(o =>
        {
            o.EnableRecording = true;
            o.OnRecordingCompleteAsync = (_, b) => { wav = b; return Task.CompletedTask; };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("ai-audio");   // records 20 bytes, IsAiSpeaking → true
        await Task.Delay(30);

        FakeWs.EnqueueClientMessage("f1");                             // while speaking → suppressed, but forwarded
        await Task.Delay(60);

        await FakeWssClient.SimulateMessageReceivedAsync("audio-done"); // IsAiSpeaking → false
        await Task.Delay(30);

        FakeWs.EnqueueClientMessage("f2");                             // not speaking → recorded + forwarded
        await Task.Delay(60);

        FakeWs.EnqueueClose();
        await sessionTask;

        // (a) Suppression: recorded PCM = AI audio (20) + frame2 (12) only; frame1 was suppressed.
        //     WAV = 46-byte NAudio PCM header (18-byte fmt chunk) + 32 PCM bytes = 78.
        //     If frame1 had leaked in: 46 + 44 = 90.
        wav.ShouldNotBeNull();
        wav!.Length.ShouldBe(78);

        // (b) Forwarding is independent of recording: BOTH frames were forwarded to the provider,
        //     including frame1 which arrived while the AI was speaking.
        ProviderAdapter.Received().BuildAudioAppendMessage(Arg.Is<RealtimeAiWssAudioData>(d => d.Base64Payload == frame1));
        ProviderAdapter.Received().BuildAudioAppendMessage(Arg.Is<RealtimeAiWssAudioData>(d => d.Base64Payload == frame2));
    }
}
