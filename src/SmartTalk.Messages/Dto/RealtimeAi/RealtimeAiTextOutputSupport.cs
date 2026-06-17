namespace SmartTalk.Messages.Dto.RealtimeAi;

/// <summary>
/// Declares how an inference provider can emit text output, so the engine can decide whether to pair
/// it with an external (text-driven) TTS provider. Three-state rather than a single bool because a
/// provider may emit text only, text alongside audio, both, or neither.
/// </summary>
public sealed record RealtimeAiTextOutputSupport
{
    /// <summary>The provider can run in a text-only output mode (no audio modality).</summary>
    public required bool CanEmitTextOnly { get; init; }

    /// <summary>The provider can emit text in the same response as audio.</summary>
    public required bool CanEmitTextAlongsideAudio { get; init; }

    /// <summary>
    /// The provider-specific token for the text modality in its session payload (e.g. OpenAI's
    /// <c>"text"</c> in <c>output_modalities</c>). Declarative so the adapter does not hardcode it
    /// at the call site. Null when the provider has no text modality.
    /// </summary>
    public string TextModalityToken { get; init; }
}
