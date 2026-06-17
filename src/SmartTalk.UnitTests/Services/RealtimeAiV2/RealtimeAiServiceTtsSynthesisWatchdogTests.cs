using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Core.Services.RealtimeAiV2.Watchdog;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Resilience backstop: an external TTS provider that never raises a terminal signal (SynthesisCompleted
/// /Failed) would leave the dual gate waiting forever after the inference provider's turn is done. The
/// TTS-synthesis watchdog force-completes such a wedged turn after a ceiling, through the SAME exactly-once
/// gate the real signal uses (so a late real signal cannot double-complete). Built-in audio mode never
/// arms the watchdog. Also pins the operator-override env-var name and default (house rule 8).
/// </summary>
public class RealtimeAiServiceTtsSynthesisWatchdogTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task ExternalTts_NeverSignalsCompletion_WatchdogForceCompletesTheTurnOnce()
    {
        var tts = new NeverCompletingTextTtsProvider();
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

        var options = CreateDefaultOptions(o =>
        {
            o.TtsConfig = new RealtimeAiTtsConfig { ProviderType = RealtimeAiTtsProviderType.MiniMax, SampleRate = 24000 };
            o.TtsSynthesisTimeout = TimeSpan.FromMilliseconds(150);   // short backstop for the test
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("delta");   // CurrentResponseHasTextOutput = true
        await FakeWssClient.SimulateMessageReceivedAsync("done");    // provider turn done; TTS will never signal → watchdog armed

        // Before the ceiling the turn is still waiting (the wedge that would otherwise hang forever).
        TurnCompletedCount().ShouldBe(0);

        await Task.Delay(400);   // exceed the 150ms ceiling

        TurnCompletedCount().ShouldBe(1);   // watchdog force-completed the wedged turn, exactly once

        FakeWs.EnqueueClose();
        await sessionTask;
    }

    [Fact]
    public async Task ExternalTts_ProviderStreamsTextThenStalls_HardCeilingForceCompletesTheTurn()
    {
        var tts = new NeverCompletingTextTtsProvider();
        Switcher.TtsProvider(RealtimeAiTtsProviderType.MiniMax).Returns(tts);

        ProviderAdapter.ParseMessage("delta").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTextDelta,
            Data = new RealtimeAiWssTextData { Text = "hi" }
        });

        var options = CreateDefaultOptions(o =>
        {
            o.TtsConfig = new RealtimeAiTtsConfig { ProviderType = RealtimeAiTtsProviderType.MiniMax, SampleRate = 24000 };
            o.TurnHardCeiling = TimeSpan.FromMilliseconds(150);
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        // First text arms the hard ceiling; the provider then STALLS — no response.done ever arrives, so the
        // TTS-synthesis watchdog is never armed and only the hard ceiling can rescue the turn.
        await FakeWssClient.SimulateMessageReceivedAsync("delta");

        TurnCompletedCount().ShouldBe(0);

        await Task.Delay(400);

        TurnCompletedCount().ShouldBe(1);

        FakeWs.EnqueueClose();
        await sessionTask;
    }

    [Fact]
    public void WatchdogEnvVars_NamesAndDefaultsPinned()
    {
        // Renaming these breaks any air-gapped operator who pinned the timeouts via env — hard-pin them.
        RealtimeAiTurnWatchdogDefaults.TtsSynthesisTimeoutEnvVar.ShouldBe("SMARTTALK_REALTIME_TTS_SYNTHESIS_TIMEOUT_MS");
        RealtimeAiTurnWatchdogDefaults.DefaultTtsSynthesisTimeoutMs.ShouldBe(8000);
        RealtimeAiTurnWatchdogDefaults.TurnHardCeilingEnvVar.ShouldBe("SMARTTALK_REALTIME_TURN_HARD_CEILING_MS");
        RealtimeAiTurnWatchdogDefaults.DefaultTurnHardCeilingMs.ShouldBe(45000);
    }

    private int TurnCompletedCount() => FakeWs.GetSentTextMessages().Count(m => m.Contains("AiTurnCompleted"));

    private sealed class NeverCompletingTextTtsProvider : IRealtimeAiTtsProvider, IRealtimeAiTextSynthesizer
    {
        public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.MiniMax;
        public RealtimeAiAudioCodec OutputCodec => RealtimeAiAudioCodec.PCM16;
        public int OutputSampleRate => 24000;

        public event Func<string, Task> AudioChunkReadyAsync { add { } remove { } }
        public event Func<Task> SynthesisCompletedAsync { add { } remove { } }       // never raised → the wedge
        public event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync { add { } remove { } }

        public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleProviderTextDeltaAsync(string textDelta, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleProviderTextDoneAsync(CancellationToken cancellationToken) => Task.CompletedTask;   // does NOT complete
        public Task HandleInterruptAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
