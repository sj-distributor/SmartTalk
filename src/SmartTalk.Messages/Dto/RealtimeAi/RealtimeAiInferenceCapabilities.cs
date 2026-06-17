using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Messages.Dto.RealtimeAi;

/// <summary>
/// What an inference provider adapter can do, declared as data the engine reads instead of branching
/// on a specific vendor. The engine uses this to negotiate the output mode (audio vs text) and to
/// validate codec compatibility at session start.
/// </summary>
public sealed record RealtimeAiInferenceCapabilities
{
    /// <summary>How (and whether) the provider can emit text output.</summary>
    public required RealtimeAiTextOutputSupport TextOutput { get; init; }

    /// <summary>The provider can emit native audio output.</summary>
    public required bool SupportsAudioOutput { get; init; }

    /// <summary>Client/input audio codecs the provider accepts.</summary>
    public required IReadOnlySet<RealtimeAiAudioCodec> InputCodecs { get; init; }

    /// <summary>Audio codecs the provider can produce on output.</summary>
    public required IReadOnlySet<RealtimeAiAudioCodec> OutputCodecs { get; init; }
}
