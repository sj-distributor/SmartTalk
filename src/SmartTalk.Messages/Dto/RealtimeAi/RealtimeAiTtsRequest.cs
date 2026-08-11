namespace SmartTalk.Messages.Dto.RealtimeAi;

/// <summary>
/// What a consumer asks the TTS config resolver to build a <see cref="RealtimeAiTtsConfig"/> from — the
/// per-assistant intent, independent of which TTS vendor (if any) ends up serving it. A vendor config
/// source reads this and returns a config when it applies to the assistant, or null when it does not.
/// </summary>
public sealed class RealtimeAiTtsRequest
{
    public int AssistantId { get; init; }

    public string ModelVoice { get; init; }

    /// <summary>Desired output sample rate; null lets the vendor source use its own configured default.</summary>
    public int? SampleRate { get; init; }

    /// <summary>Optional source-sample-rate hint forwarded into the vendor's provider-specific config.</summary>
    public int? SourceSampleRate { get; init; }
}
