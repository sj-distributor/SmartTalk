using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Fakes;

/// <summary>
/// A TTS provider whose <see cref="SynthesisFailedAsync"/> event can actually be raised.
///
/// <para>Every other double in this suite uses <c>BuiltInRealtimeAiTtsProvider</c>, whose
/// <c>SynthesisFailedAsync</c> is declared with empty add/remove accessors
/// (BuiltInRealtimeAiTtsProvider.cs:19-23) — it structurally discards every subscription. The
/// engine's only non-watchdog handler for a TTS failure
/// (<c>RealtimeAiService.OnTtsSynthesisFailedAsync</c>) was therefore unreachable from any test,
/// even though it can send an error frame to the client and tear down a live session.</para>
///
/// <para>Reports as <see cref="RealtimeAiTtsProviderType.BuiltIn"/> so the output-mode negotiator
/// resolves Audio — the production path for every phone call.</para>
/// </summary>
public class FakeFailingTtsProvider : IRealtimeAiTtsProvider, IRealtimeAiAudioPassthrough
{
    public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.BuiltIn;

    public RealtimeAiAudioCodec OutputCodec { get; private set; } = RealtimeAiAudioCodec.PCM16;

    public int OutputSampleRate { get; private set; } = 24000;

    public event Func<string, Task>? AudioChunkReadyAsync;

    public event Func<Task>? SynthesisCompletedAsync;

    public event Func<RealtimeAiErrorData, Task>? SynthesisFailedAsync;

    public int InterruptCount { get; private set; }

    public int StopCount { get; private set; }

    /// <summary>Raise the terminal failure signal the engine subscribes to.</summary>
    public Task SimulateSynthesisFailedAsync(RealtimeAiErrorData? errorData) =>
        SynthesisFailedAsync?.Invoke(errorData!) ?? Task.CompletedTask;

    public Task SimulateSynthesisCompletedAsync() => SynthesisCompletedAsync?.Invoke() ?? Task.CompletedTask;

    public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken)
    {
        OutputCodec = config.TargetCodec;
        OutputSampleRate = config.SampleRate ?? 24000;

        return Task.CompletedTask;
    }

    public Task HandleProviderAudioAsync(string base64Audio, CancellationToken cancellationToken) =>
        AudioChunkReadyAsync?.Invoke(base64Audio) ?? Task.CompletedTask;

    public Task HandleProviderAudioDoneAsync(CancellationToken cancellationToken) =>
        SynthesisCompletedAsync?.Invoke() ?? Task.CompletedTask;

    public Task HandleInterruptAsync(CancellationToken cancellationToken)
    {
        InterruptCount++;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopCount++;
        return Task.CompletedTask;
    }
}
