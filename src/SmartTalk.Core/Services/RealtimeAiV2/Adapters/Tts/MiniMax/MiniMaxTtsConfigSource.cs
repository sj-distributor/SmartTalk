using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.Config;
using SmartTalk.Core.Settings.MiniMax;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.MiniMax;

/// <summary>
/// MiniMax's TTS config source. Wraps <see cref="MiniMaxTtsSettings"/> so the existing per-assistant
/// enable/api-key gating is preserved verbatim — it returns null when MiniMax is not enabled for the
/// assistant, leaving the engine on the built-in audio path. A request with no explicit sample rate
/// falls back to MiniMax's own configured rate (matching the prior consumer behaviour).
/// </summary>
public sealed class MiniMaxTtsConfigSource : IRealtimeAiTtsConfigSource
{
    private readonly MiniMaxTtsSettings _settings;

    public MiniMaxTtsConfigSource(MiniMaxTtsSettings settings)
    {
        _settings = settings;
    }

    public RealtimeAiTtsConfig Build(RealtimeAiTtsRequest request) =>
        _settings.BuildRealtimeAiTtsConfig(
            request.AssistantId,
            request.ModelVoice,
            request.SampleRate ?? _settings.SampleRate,
            request.SourceSampleRate);
}
