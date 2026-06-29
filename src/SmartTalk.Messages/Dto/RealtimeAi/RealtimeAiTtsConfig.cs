using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Messages.Dto.RealtimeAi;

/// <summary>
/// Optional text-to-speech provider configuration. Null or BuiltIn preserves the
/// existing provider-owned audio path; non-BuiltIn providers receive provider text
/// output and emit audio back through the generic realtime pipeline.
/// </summary>
public class RealtimeAiTtsConfig
{
    public RealtimeAiTtsProviderType ProviderType { get; set; } = RealtimeAiTtsProviderType.BuiltIn;

    public string Voice { get; set; }

    public string ServiceUrl { get; set; }

    public string ApiKey { get; set; }

    public RealtimeAiAudioCodec TargetCodec { get; set; } = RealtimeAiAudioCodec.PCM16;

    public int? SampleRate { get; set; }

    public Dictionary<string, object> ProviderSpecificConfig { get; set; } = new();
}
