using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.Config;

/// <summary>
/// A per-vendor builder of <see cref="RealtimeAiTtsConfig"/> from a <see cref="RealtimeAiTtsRequest"/>.
/// Self-gating: <see cref="Build"/> returns null when this vendor does not apply to the request (e.g.
/// not enabled for the assistant). A new TTS vendor adds one implementation here — auto-registered via
/// <see cref="IScopedDependency"/> — with no edit to the consumers or the resolver.
/// </summary>
public interface IRealtimeAiTtsConfigSource : IScopedDependency
{
    RealtimeAiTtsProviderType ProviderType { get; }

    RealtimeAiTtsConfig Build(RealtimeAiTtsRequest request);
}
