using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.Config;

/// <summary>
/// Resolves the external TTS config for a request by asking each registered vendor source in turn and
/// returning the first that applies (null = no external TTS → the engine uses the built-in audio path).
/// Consumers depend on this instead of any concrete vendor settings, so adding a TTS vendor is purely a
/// new <see cref="IRealtimeAiTtsConfigSource"/> with no consumer change.
/// </summary>
public sealed class RealtimeAiTtsConfigResolver : IScopedDependency
{
    private readonly IEnumerable<IRealtimeAiTtsConfigSource> _sources;

    public RealtimeAiTtsConfigResolver(IEnumerable<IRealtimeAiTtsConfigSource> sources)
    {
        _sources = sources;
    }

    public RealtimeAiTtsConfig Resolve(RealtimeAiTtsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var source in _sources)
        {
            var config = source.Build(request);

            if (config != null) return config;
        }

        return null;
    }
}
