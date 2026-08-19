using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Fakes;

/// <summary>
/// An external (text-input) TTS provider whose terminal signal the test fires by hand.
///
/// <para>Real synthesizers raise <c>SynthesisCompleted</c> from their own socket's receive loop,
/// which is what makes a late signal from a superseded turn possible at all. Nothing in the event
/// says which turn it belongs to, so a test needs to be able to raise it at a chosen moment.</para>
///
/// <para>Reports MiniMax so the output-mode negotiator resolves Text and the engine's dual turn gate
/// actually waits on this leg.</para>
/// </summary>
public class FakeTextSynthesizerTtsProvider : IRealtimeAiTtsProvider, IRealtimeAiTextSynthesizer
{
    public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.MiniMax;

    public RealtimeAiAudioCodec OutputCodec { get; private set; } = RealtimeAiAudioCodec.PCM16;

    public int OutputSampleRate { get; private set; } = 24000;

    public event Func<string, Task>? AudioChunkReadyAsync;

    public event Func<Task>? SynthesisCompletedAsync;

    public event Func<RealtimeAiErrorData, Task>? SynthesisFailedAsync;

    public List<string> ReceivedText { get; } = new();

    public int TextDoneCount { get; private set; }

    /// <summary>Raise the terminal signal, as the provider's own receive loop would.</summary>
    public Task SimulateSynthesisCompletedAsync() => SynthesisCompletedAsync?.Invoke() ?? Task.CompletedTask;

    public Task SimulateAudioChunkAsync(string base64) => AudioChunkReadyAsync?.Invoke(base64) ?? Task.CompletedTask;

    public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken)
    {
        OutputCodec = config.TargetCodec;
        OutputSampleRate = config.SampleRate ?? 24000;

        return Task.CompletedTask;
    }

    public Task HandleProviderTextDeltaAsync(string text, CancellationToken cancellationToken)
    {
        ReceivedText.Add(text);
        return Task.CompletedTask;
    }

    public Task HandleProviderTextDoneAsync(CancellationToken cancellationToken)
    {
        // Deliberately does NOT auto-complete: the whole point is to control when the terminal
        // signal lands relative to the next turn starting.
        TextDoneCount++;
        return Task.CompletedTask;
    }

    public Task HandleInterruptAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
