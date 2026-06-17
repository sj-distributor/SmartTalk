using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// Pins the S5 gate: in audio mode (OutputMode == Audio), a response.done that carries assistant
/// transcript text must NOT drive the external-TTS text-synthesis path (the BuiltIn provider's text
/// handlers stay untouched; the transcript arrives via output_audio_transcript events instead), while
/// the turn still completes exactly once. Production behaviour with the real no-op BuiltIn provider is
/// unchanged — this only proves the audio path stops executing text-mode routing. The text path itself
/// remains covered by RealtimeAiServiceExternalTtsTests.
/// </summary>
public class RealtimeAiServiceAudioModeTextFlushGateGoldenTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task AudioMode_ResponseDoneWithAssistantText_DoesNotRouteToTtsTextPath_AndCompletesOnce()
    {
        var tts = new RecordingBuiltInTtsProvider();
        Switcher.TtsProvider(Arg.Any<RealtimeAiTtsProviderType>()).Returns(tts);   // BuiltIn → negotiator → audio mode

        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTurnCompleted,
            Data = new RealtimeAiWssTextData { Text = "spoken transcript" }   // as the OpenAI adapter extracts in audio mode
        });

        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("done-with-text");

        FakeWs.EnqueueClose();
        await sessionTask;

        tts.TextDeltaCalls.ShouldBe(0);
        tts.TextDoneCalls.ShouldBe(0);
        FakeWs.GetSentTextMessages().Count(m => m.Contains("AiTurnCompleted")).ShouldBe(1);
    }

    /// <summary>BuiltIn-type provider that ALSO implements the text-synthesizer sibling, so text routing
    /// is structurally possible and the S5 gate (not S6's interface split) is what we observe: in audio
    /// mode the engine must still not drive the text handlers.</summary>
    private sealed class RecordingBuiltInTtsProvider : IRealtimeAiTtsProvider, IRealtimeAiAudioPassthrough, IRealtimeAiTextSynthesizer
    {
        public int TextDeltaCalls;
        public int TextDoneCalls;

        public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.BuiltIn;
        public RealtimeAiAudioCodec OutputCodec { get; private set; } = RealtimeAiAudioCodec.PCM16;
        public int OutputSampleRate { get; private set; } = 24000;

        public event Func<string, Task>? AudioChunkReadyAsync;
        public event Func<Task>? SynthesisCompletedAsync;
        public event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync { add { } remove { } }

        public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken)
        {
            OutputCodec = config.TargetCodec;
            OutputSampleRate = config.SampleRate ?? AudioCodecConverter.GetSampleRate(OutputCodec);
            return Task.CompletedTask;
        }

        public Task HandleProviderAudioAsync(string base64Audio, CancellationToken cancellationToken) =>
            AudioChunkReadyAsync?.Invoke(base64Audio) ?? Task.CompletedTask;

        public Task HandleProviderAudioDoneAsync(CancellationToken cancellationToken) =>
            SynthesisCompletedAsync?.Invoke() ?? Task.CompletedTask;

        public Task HandleProviderTextDeltaAsync(string textDelta, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref TextDeltaCalls);
            return Task.CompletedTask;
        }

        public Task HandleProviderTextDoneAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref TextDoneCalls);
            return SynthesisCompletedAsync?.Invoke() ?? Task.CompletedTask;
        }

        public Task HandleInterruptAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
