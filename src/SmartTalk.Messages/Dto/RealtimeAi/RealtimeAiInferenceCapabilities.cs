namespace SmartTalk.Messages.Dto.RealtimeAi;

/// <summary>
/// What an inference provider adapter can do, declared as data the engine reads instead of branching on a
/// specific vendor. The engine uses this to negotiate the output mode (audio vs text) at session start.
/// </summary>
public sealed record RealtimeAiInferenceCapabilities
{
    /// <summary>How (and whether) the provider can emit text output.</summary>
    public required RealtimeAiTextOutputSupport TextOutput { get; init; }

    /// <summary>The provider can emit native audio output.</summary>
    public required bool SupportsAudioOutput { get; init; }
}
