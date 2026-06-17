using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;

/// <summary>
/// Base contract every TTS provider implements: identity, output audio format, the events the engine
/// subscribes to, and lifecycle. The DIRECTION-specific input handlers live on the sibling capability
/// interfaces <see cref="IRealtimeAiAudioPassthrough"/> (audio output mode) and
/// <see cref="IRealtimeAiTextSynthesizer"/> (text output mode); a provider implements exactly the one
/// matching its output mode, so neither carries a dead no-op for the half it doesn't use.
///
/// Terminal-signal contract: for every AI turn the provider MUST eventually raise exactly one of
/// <see cref="SynthesisCompletedAsync"/> or <see cref="SynthesisFailedAsync"/>;
/// <see cref="AudioChunkReadyAsync"/> may fire 0..N times before it.
/// </summary>
public interface IRealtimeAiTtsProvider : IScopedDependency
{
    RealtimeAiTtsProviderType TtsProviderType { get; }

    RealtimeAiAudioCodec OutputCodec { get; }

    int OutputSampleRate { get; }

    event Func<string, Task> AudioChunkReadyAsync;

    event Func<Task> SynthesisCompletedAsync;

    event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync;

    Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken);

    Task HandleInterruptAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
