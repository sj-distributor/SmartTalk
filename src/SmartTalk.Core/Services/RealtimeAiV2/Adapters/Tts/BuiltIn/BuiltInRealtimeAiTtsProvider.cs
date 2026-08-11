using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.BuiltIn;

public class BuiltInRealtimeAiTtsProvider : IRealtimeAiTtsProvider, IRealtimeAiAudioPassthrough
{
    public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.BuiltIn;

    public RealtimeAiAudioCodec OutputCodec { get; private set; } = RealtimeAiAudioCodec.PCM16;

    public int OutputSampleRate { get; private set; } = 24000;

    public event Func<string, Task> AudioChunkReadyAsync;

    public event Func<Task> SynthesisCompletedAsync;

    public event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync
    {
        add { }
        remove { }
    }

    public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken)
    {
        OutputCodec = config.TargetCodec;
        OutputSampleRate = config.SampleRate ?? AudioCodecConverter.GetSampleRate(OutputCodec);

        return Task.CompletedTask;
    }

    public Task HandleProviderAudioAsync(string base64Audio, CancellationToken cancellationToken)
    {
        return AudioChunkReadyAsync?.Invoke(base64Audio) ?? Task.CompletedTask;
    }

    public Task HandleProviderAudioDoneAsync(CancellationToken cancellationToken)
    {
        return SynthesisCompletedAsync?.Invoke() ?? Task.CompletedTask;
    }

    public Task HandleInterruptAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
