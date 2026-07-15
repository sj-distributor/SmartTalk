namespace SmartTalk.Messages.Enums.RealtimeAi;

/// <summary>
/// The output modality the engine negotiates for an inference provider session: native audio (the
/// provider speaks) or text (the provider emits text that an external TTS provider voices). Decided
/// once at session start by the OutputModeNegotiator and passed explicitly to BuildSessionConfig, so
/// the inference adapter never infers it from TTS configuration.
/// </summary>
public enum RealtimeAiOutputMode
{
    Audio,
    Text
}
