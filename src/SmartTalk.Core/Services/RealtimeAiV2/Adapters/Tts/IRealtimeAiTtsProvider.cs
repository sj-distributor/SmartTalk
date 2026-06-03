using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;

public interface IRealtimeAiTtsProvider : IScopedDependency
{
    RealtimeAiTtsProviderType TtsProviderType { get; }

    RealtimeAiAudioCodec OutputCodec { get; }

    int OutputSampleRate { get; }

    event Func<string, Task> AudioChunkReadyAsync;

    event Func<Task> SynthesisCompletedAsync;

    event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync;

    Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken);

    Task HandleProviderAudioAsync(string base64Audio, CancellationToken cancellationToken);

    Task HandleProviderTextDeltaAsync(string textDelta, CancellationToken cancellationToken);

    Task HandleProviderTextDoneAsync(CancellationToken cancellationToken);

    Task HandleInterruptAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
