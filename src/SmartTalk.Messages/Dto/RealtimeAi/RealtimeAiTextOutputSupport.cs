namespace SmartTalk.Messages.Dto.RealtimeAi;

/// <summary>
/// Declares how an inference provider can emit text output, so the engine can decide whether to pair it
/// with an external (text-driven) TTS provider. Two flags rather than one bool because a provider may emit
/// text only, text alongside audio, both, or neither.
/// </summary>
public sealed record RealtimeAiTextOutputSupport
{
    /// <summary>The provider can run in a text-only output mode (no audio modality).</summary>
    public required bool CanEmitTextOnly { get; init; }

    /// <summary>The provider can emit text in the same response as audio.</summary>
    public required bool CanEmitTextAlongsideAudio { get; init; }
}
