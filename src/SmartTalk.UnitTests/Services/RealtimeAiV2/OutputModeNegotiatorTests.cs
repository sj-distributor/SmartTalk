using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Negotiation;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The single point of coupling between the inference and TTS axes (S4). Pins that a compatible pairing
/// resolves to the right output mode and an incompatible one FAILS LOUD — the negotiator is what stops a
/// non-text-capable inference provider paired with external TTS from producing a silently mute call (D1).
/// </summary>
public class OutputModeNegotiatorTests
{
    private static RealtimeAiInferenceCapabilities Caps(bool textOnly, bool textAlongside, bool audio) => new()
    {
        TextOutput = new RealtimeAiTextOutputSupport { CanEmitTextOnly = textOnly, CanEmitTextAlongsideAudio = textAlongside },
        SupportsAudioOutput = audio,
        InputCodecs = new HashSet<RealtimeAiAudioCodec> { RealtimeAiAudioCodec.PCM16 },
        OutputCodecs = new HashSet<RealtimeAiAudioCodec> { RealtimeAiAudioCodec.PCM16 }
    };

    [Theory]
    // built-in (audio passthrough) TTS + audio-capable inference → Audio
    [InlineData(false, false, true, false, RealtimeAiOutputMode.Audio)]
    // external (text) TTS + text-only-capable inference → Text
    [InlineData(true, false, true, true, RealtimeAiOutputMode.Text)]
    // external TTS + text-alongside-audio-capable inference → Text
    [InlineData(false, true, true, true, RealtimeAiOutputMode.Text)]
    public void Resolve_CompatiblePairing_ReturnsExpectedMode(bool textOnly, bool textAlongside, bool audio, bool ttsRequiresText, RealtimeAiOutputMode expected)
    {
        OutputModeNegotiator.Resolve(Caps(textOnly, textAlongside, audio), ttsRequiresText).ShouldBe(expected);
    }

    [Fact]
    public void Resolve_ExternalTtsWithNonTextCapableInference_FailsLoud()
    {
        Should.Throw<RealtimeAiOutputModeException>(() =>
            OutputModeNegotiator.Resolve(Caps(textOnly: false, textAlongside: false, audio: true), ttsRequiresTextInput: true));
    }

    [Fact]
    public void Resolve_PassthroughTtsWithNonAudioInference_FailsLoud()
    {
        Should.Throw<RealtimeAiOutputModeException>(() =>
            OutputModeNegotiator.Resolve(Caps(textOnly: true, textAlongside: false, audio: false), ttsRequiresTextInput: false));
    }
}
