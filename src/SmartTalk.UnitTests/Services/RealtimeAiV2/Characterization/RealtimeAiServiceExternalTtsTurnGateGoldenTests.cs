using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the external-TTS dual-gate completion when TTS synthesis finishes
/// BEFORE the provider turn-done (the order the existing FakeExternalTtsProvider cannot produce, since
/// it raises synthesis-completed from within HandleProviderTextDoneAsync). Both orders must converge to
/// exactly one turn-completion. Migration steps S6 (provider split) and S9–S12 (turn-stamped gate /
/// single _turnLock) rewrite TryMarkCurrentResponseTurnCompletedLocked; this order-independence and the
/// exactly-once guard must survive, or it fails RED.
/// </summary>
public class RealtimeAiServiceExternalTtsTurnGateGoldenTests : RealtimeAiServiceTestBase
{
    private int TurnCompletedCount() => FakeWs.GetSentTextMessages().Count(m => m.Contains("AiTurnCompleted"));

    [Fact]
    public async Task ExternalTts_SynthesisDoneBeforeProviderDone_CompletesExactlyOnceOnProviderDone()
    {
        var tts = new ManualExternalTtsProvider();
        Switcher.TtsProvider(RealtimeAiTtsProviderType.MiniMax).Returns(tts);

        ProviderAdapter.ParseMessage("delta").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTextDelta,
            Data = new RealtimeAiWssTextData { Text = "hi" }
        });
        ProviderAdapter.ParseMessage("done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTurnCompleted
        });

        var options = CreateDefaultOptions(o => o.TtsConfig = new RealtimeAiTtsConfig
        {
            ProviderType = RealtimeAiTtsProviderType.MiniMax,
            SampleRate = 24000
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("delta");      // CurrentResponseHasTextOutput = true

        // TTS synthesis completes FIRST — provider turn-done has not arrived, so the turn must NOT complete.
        await tts.RaiseSynthesisCompletedAsync();
        TurnCompletedCount().ShouldBe(0);

        // Provider turn-done arrives second — both gates are now satisfied → complete exactly once.
        await FakeWssClient.SimulateMessageReceivedAsync("done");
        TurnCompletedCount().ShouldBe(1);

        FakeWs.EnqueueClose();
        await sessionTask;
    }

    /// <summary>External TTS fake whose synthesis-completed signal the test raises explicitly, so the
    /// gate order (synthesis-done before provider-done) can be controlled. HandleProviderTextDoneAsync
    /// does NOT auto-complete, to avoid a second terminal signal.</summary>
    private sealed class ManualExternalTtsProvider : IRealtimeAiTtsProvider
    {
        public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.MiniMax;
        public RealtimeAiAudioCodec OutputCodec => RealtimeAiAudioCodec.PCM16;
        public int OutputSampleRate { get; private set; } = 24000;

        public event Func<string, Task>? AudioChunkReadyAsync { add { } remove { } }
        public event Func<Task>? SynthesisCompletedAsync;
        public event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync { add { } remove { } }

        public Task RaiseSynthesisCompletedAsync() => SynthesisCompletedAsync?.Invoke() ?? Task.CompletedTask;

        public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken)
        {
            OutputSampleRate = config.SampleRate ?? 24000;
            return Task.CompletedTask;
        }

        public Task HandleProviderAudioAsync(string base64Audio, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleProviderTextDeltaAsync(string textDelta, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleProviderTextDoneAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleInterruptAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
