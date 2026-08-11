using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Negotiation;

/// <summary>
/// Decides the session output mode from the inference provider's declared capabilities and whether the
/// TTS provider needs text input. The single, pure point of coupling between the inference and TTS
/// axes: it fails LOUD on an incompatible pairing rather than letting a non-text-capable inference
/// provider drive an external TTS into a silently mute call.
/// </summary>
public static class OutputModeNegotiator
{
    public static RealtimeAiOutputMode Resolve(RealtimeAiInferenceCapabilities inference, bool ttsRequiresTextInput)
    {
        ArgumentNullException.ThrowIfNull(inference);

        if (!ttsRequiresTextInput)
        {
            if (!inference.SupportsAudioOutput)
                throw new RealtimeAiOutputModeException("Built-in TTS requires native audio output, but the inference provider cannot emit audio.");

            return RealtimeAiOutputMode.Audio;
        }

        if (!inference.TextOutput.CanEmitTextOnly && !inference.TextOutput.CanEmitTextAlongsideAudio)
            throw new RealtimeAiOutputModeException("External TTS requires text output, but the inference provider cannot emit text.");

        return RealtimeAiOutputMode.Text;
    }
}

public sealed class RealtimeAiOutputModeException : Exception
{
    public RealtimeAiOutputModeException(string message) : base(message) { }
}
