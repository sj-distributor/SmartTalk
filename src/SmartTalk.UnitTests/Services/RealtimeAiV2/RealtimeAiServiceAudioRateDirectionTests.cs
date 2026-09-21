using System.Collections.Concurrent;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The two audio legs of a call need two different sample rates, and the engine was resolving the
/// wrong one for the uplink: the rate the caller's microphone audio is resampled TO was read off the
/// VOICE provider's playback rate, which has nothing to do with what the inference provider accepts.
///
/// <para>Latent rather than active. On the production path TtsConfig is null, so the built-in
/// passthrough reports the nominal rate of the negotiated codec — 8000 for mu-law, the same number the
/// client speaks — and the transcode short-circuits. MiniMax's own default is also 8000. Both are
/// correct only by coincidence, and the coincidence breaks the moment anyone raises a voice provider's
/// sample rate for audio quality, which silently corrupts the speech going INTO the model with no log
/// line saying anything was resampled.</para>
///
/// <para>No test could see this: the shared harness fixes the client codec at PCM16 and the built-in
/// provider reports 24000 for it, so source and target are equal in every one of the existing tests
/// and the whole transcode path is short-circuited. These two drive it with the real telephony shape.</para>
/// </summary>
public class RealtimeAiServiceAudioRateDirectionTests : RealtimeAiServiceTestBase
{
    /// <summary>A passthrough voice whose playback rate is deliberately NOT the nominal rate of its codec.</summary>
    private sealed class PassthroughVoiceAt : IRealtimeAiTtsProvider, IRealtimeAiAudioPassthrough
    {
        public RealtimeAiAudioCodec OutputCodec { get; init; } = RealtimeAiAudioCodec.MULAW;
        public int OutputSampleRate { get; init; } = 24000;

        public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.MiniMax;

        public event Func<string, Task> AudioChunkReadyAsync;
        public event Func<Task> SynthesisCompletedAsync { add { } remove { } }
        public event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync { add { } remove { } }

        public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleProviderAudioAsync(string base64Audio, CancellationToken cancellationToken) => AudioChunkReadyAsync?.Invoke(base64Audio) ?? Task.CompletedTask;
        public Task HandleProviderAudioDoneAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleInterruptAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private RealtimeSessionOptions TelephonyOptionsUsing(IRealtimeAiTtsProvider voice)
    {
        // The real telephony shape: Twilio speaks mu-law at 8 kHz and OpenAI accepts it unchanged.
        ClientAdapter.NativeAudioCodec.Returns(RealtimeAiAudioCodec.MULAW);
        ProviderAdapter.GetPreferredCodec(Arg.Any<RealtimeAiAudioCodec>()).Returns(ci => ci.ArgAt<RealtimeAiAudioCodec>(0));
        Switcher.TtsProvider(RealtimeAiTtsProviderType.MiniMax).Returns(voice);

        return CreateDefaultOptions(o =>
            o.TtsConfig = new RealtimeAiTtsConfig { ProviderType = RealtimeAiTtsProviderType.MiniMax, SampleRate = 24000 });
    }

    private ConcurrentQueue<string> CaptureUplinkPayloads()
    {
        var seen = new ConcurrentQueue<string>();

        ProviderAdapter.BuildAudioAppendMessage(Arg.Any<RealtimeAiWssAudioData>()).Returns(ci =>
        {
            seen.Enqueue(ci.Arg<RealtimeAiWssAudioData>().Base64Payload);
            return "audio_append";
        });

        return seen;
    }

    private ConcurrentQueue<string> CaptureDownlinkPayloads()
    {
        var seen = new ConcurrentQueue<string>();

        ClientAdapter.BuildAudioDeltaMessage(Arg.Any<string>(), Arg.Any<string>()).Returns(ci =>
        {
            seen.Enqueue(ci.ArgAt<string>(0));
            return "audio_delta";
        });

        return seen;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(20);
        }

        throw new Xunit.Sdk.XunitException($"Timed out after 5s waiting for: {description}");
    }

    [Fact]
    public async Task UplinkAudio_ShouldKeepTheRateTheInferenceProviderAccepts()
    {
        // The caller's mu-law at 8 kHz is what OpenAI was told to expect, so it must arrive untouched.
        // Resampled to the voice's 24 kHz playback rate and re-encoded as mu-law, it reaches the model
        // three times too long and pitched down — transcription and turn detection both fail.
        var uplink = CaptureUplinkPayloads();
        var frame = Convert.ToBase64String(Enumerable.Range(0, 160).Select(i => (byte)(i % 251)).ToArray());

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = frame });

        var sessionTask = await StartSessionInBackgroundAsync(TelephonyOptionsUsing(new PassthroughVoiceAt()));
        FakeWs.EnqueueClientMessage("audio-frame");

        await WaitUntilAsync(() => uplink.Count == 1, "the caller's frame to reach the inference provider");

        uplink.Single().ShouldBe(frame, "the caller's audio is already in the format the model was told to expect");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DownlinkAudio_ShouldBeResampledFromTheVoicesOwnRateNotItsCodecsNominalRate()
    {
        // The guard against the tempting simplification. Collapsing both legs onto the codec's nominal
        // rate compiles, leaves no warning, and passes every other test — but it silences the one thing
        // only the provider knows: a voice may emit mu-law at a rate that is not mu-law's nominal 8 kHz,
        // and reading the nominal rate then skips the downsample and plays it back at three times speed.
        var downlink = CaptureDownlinkPayloads();
        var voiceFrame = Convert.ToBase64String(Enumerable.Range(0, 480).Select(i => (byte)(i % 251)).ToArray());

        var sessionTask = await StartSessionInBackgroundAsync(TelephonyOptionsUsing(new PassthroughVoiceAt()));

        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseAudioDelta,
            Data = new RealtimeAiWssAudioData { Base64Payload = voiceFrame }
        });

        await FakeWssClient.SimulateMessageReceivedAsync("audio");

        await WaitUntilAsync(() => downlink.Count == 1, "the voice's frame to reach the caller");

        Convert.FromBase64String(downlink.Single()).Length
            .ShouldBe(160, "480 mu-law samples at the voice's 24 kHz become 160 at the caller's 8 kHz");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CodecNominalRates_ShouldStayPinned()
    {
        // Both legs are derived from these. A change here moves audio rates on every call.
        AudioCodecConverter.GetSampleRate(RealtimeAiAudioCodec.MULAW).ShouldBe(8000);
        AudioCodecConverter.GetSampleRate(RealtimeAiAudioCodec.ALAW).ShouldBe(8000);
        AudioCodecConverter.GetSampleRate(RealtimeAiAudioCodec.PCM16).ShouldBe(24000);
    }
}
