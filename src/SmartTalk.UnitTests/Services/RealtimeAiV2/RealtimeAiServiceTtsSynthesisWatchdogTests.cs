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
/// gate the real signal uses (so a late real signal cannot double-complete), and a watchdog from a
/// superseded turn stands down (it captured the old turn generation). Built-in audio mode never arms a
/// watchdog. The backstop durations are fixed engineering constants (no env/option surface); tests inject a
/// short value through RealtimeAiService's internal override seam.
/// </summary>
public class RealtimeAiServiceTtsSynthesisWatchdogTests : RealtimeAiServiceTestBase
{
    private static readonly TimeSpan ShortCeiling = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan PastCeiling = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan NeverDuringTest = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ExternalTts_NeverSignalsCompletion_WatchdogForceCompletesTheTurnOnce()
    {
        UseExternalTts(new NeverCompletingTextTtsProvider());
        Sut.TtsSynthesisWatchdogTimeoutOverride = ShortCeiling;   // short backstop for the test

        var options = CreateExternalTtsOptions();
        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("delta");   // CurrentResponseHasTextOutput = true
        await FakeWssClient.SimulateMessageReceivedAsync("done");    // provider turn done; TTS will never signal → watchdog armed

        // Before the ceiling the turn is still waiting (the wedge that would otherwise hang forever).
        TurnCompletedCount().ShouldBe(0);

        await Task.Delay(PastCeiling);   // exceed the ceiling

        TurnCompletedCount().ShouldBe(1);   // watchdog force-completed the wedged turn, exactly once

        await CloseAsync(sessionTask);
    }

    [Fact]
    public async Task ExternalTts_ProviderStreamsTextThenStalls_HardCeilingForceCompletesTheTurn()
    {
        UseExternalTts(new NeverCompletingTextTtsProvider());
        Sut.TurnHardCeilingWatchdogOverride = ShortCeiling;

        var options = CreateExternalTtsOptions();
        var sessionTask = await StartSessionInBackgroundAsync(options);

        // First text arms the hard ceiling; the provider then STALLS — no response.done ever arrives, so the
        // TTS-synthesis watchdog is never armed and only the hard ceiling can rescue the turn.
        await FakeWssClient.SimulateMessageReceivedAsync("delta");

        TurnCompletedCount().ShouldBe(0);

        await Task.Delay(PastCeiling);

        TurnCompletedCount().ShouldBe(1);

        await CloseAsync(sessionTask);
    }

    [Fact]
    public async Task RealSynthesisSignalBeforeCeiling_CompletesExactlyOnce_LateWatchdogStandsDown()
    {
        var tts = new ControllableTextTtsProvider();
        UseExternalTts(tts);
        Sut.TtsSynthesisWatchdogTimeoutOverride = TimeSpan.FromMilliseconds(200);

        var options = CreateExternalTtsOptions();
        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("delta");
        await FakeWssClient.SimulateMessageReceivedAsync("done");    // provider done; TTS pending → watchdog armed

        // The real terminal signal arrives BEFORE the ceiling → the turn completes through the normal gate.
        await tts.RaiseSynthesisCompletedAsync();

        TurnCompletedCount().ShouldBe(1);

        await Task.Delay(PastCeiling);   // the already-armed watchdog now fires late...

        TurnCompletedCount().ShouldBe(1);   // ...and stands down: the exactly-once gate blocks a second completion

        await CloseAsync(sessionTask);
    }

    [Fact]
    public async Task SupersededTtsSynthesisWatchdog_DoesNotCorruptTheNextTurn()
    {
        // A turn-N TTS-synthesis watchdog that fires after turn N+1 began must no-op (it captured the old
        // generation). If it didn't, it would mark turn N+1's TTS as completed and the next provider-done
        // would complete turn N+1 prematurely — before its real synthesis ever arrives.
        UseExternalTts(new NeverCompletingTextTtsProvider());
        Sut.TtsSynthesisWatchdogTimeoutOverride = ShortCeiling;
        Sut.TurnHardCeilingWatchdogOverride = NeverDuringTest;   // isolate the TTS-synthesis watchdog

        var options = CreateExternalTtsOptions();
        var sessionTask = await StartSessionInBackgroundAsync(options);

        // Turn N: provider done with TTS pending → turn-N watchdog armed at the current generation.
        await FakeWssClient.SimulateMessageReceivedAsync("delta");
        await FakeWssClient.SimulateMessageReceivedAsync("done");

        // Turn N+1 begins (generation bumps) and starts waiting on its own provider-done.
        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await FakeWssClient.SimulateMessageReceivedAsync("delta");

        await Task.Delay(PastCeiling);   // the stale turn-N watchdog fires here — must no-op against turn N+1

        TurnCompletedCount().ShouldBe(0);

        // Turn N+1's provider completes. Its real TTS has NOT signalled, so the turn must keep waiting — a
        // stale watchdog that corrupted the TTS-completed flag would let this complete immediately instead.
        await FakeWssClient.SimulateMessageReceivedAsync("done");

        TurnCompletedCount().ShouldBe(0);

        await CloseAsync(sessionTask);
    }

    [Fact]
    public async Task SupersededHardCeilingWatchdog_DoesNotForceCompleteALaterTurn()
    {
        // A turn-N hard-ceiling watchdog that fires after turn N+1 began must no-op. The hard ceiling forces
        // BOTH gate legs itself, so without the generation guard it would force-complete whatever turn is
        // current — completing turn N+1 with no text and no provider-done at all.
        UseExternalTts(new NeverCompletingTextTtsProvider());
        Sut.TurnHardCeilingWatchdogOverride = ShortCeiling;
        Sut.TtsSynthesisWatchdogTimeoutOverride = NeverDuringTest;

        var options = CreateExternalTtsOptions();
        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("delta");     // turn N: first text arms the hard ceiling
        await FakeWssClient.SimulateMessageReceivedAsync("started");   // turn N+1 begins → generation bumps

        await Task.Delay(PastCeiling);   // the stale turn-N hard ceiling fires here — must no-op against turn N+1

        TurnCompletedCount().ShouldBe(0);

        await CloseAsync(sessionTask);
    }

    [Fact]
    public void WatchdogDefaults_DurationsPinned()
    {
        // The backstop durations are fixed engineering safety limits, not configuration. Pin them so a change
        // is a deliberate, visible decision rather than an accidental edit to a load-bearing timing constant.
        RealtimeAiTurnWatchdogDefaults.TtsSynthesisTimeout.ShouldBe(TimeSpan.FromSeconds(8));
        RealtimeAiTurnWatchdogDefaults.TurnHardCeiling.ShouldBe(TimeSpan.FromSeconds(45));
    }

    private void UseExternalTts(IRealtimeAiTtsProvider tts) =>
        Switcher.TtsProvider(RealtimeAiTtsProviderType.MiniMax).Returns(tts);

    private RealtimeSessionOptions CreateExternalTtsOptions()
    {
        ProviderAdapter.ParseMessage("delta").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTextDelta,
            Data = new RealtimeAiWssTextData { Text = "hi" }
        });
        ProviderAdapter.ParseMessage("done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTurnCompleted
        });
        ProviderAdapter.ParseMessage("started").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseStarted
        });

        return CreateDefaultOptions(o =>
            o.TtsConfig = new RealtimeAiTtsConfig { ProviderType = RealtimeAiTtsProviderType.MiniMax, SampleRate = 24000 });
    }

    private async Task CloseAsync(Task sessionTask)
    {
        FakeWs.EnqueueClose();
        await sessionTask;
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

    private sealed class ControllableTextTtsProvider : IRealtimeAiTtsProvider, IRealtimeAiTextSynthesizer
    {
        public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.MiniMax;
        public RealtimeAiAudioCodec OutputCodec => RealtimeAiAudioCodec.PCM16;
        public int OutputSampleRate => 24000;

        public event Func<string, Task> AudioChunkReadyAsync { add { } remove { } }
        public event Func<Task> SynthesisCompletedAsync;
        public event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync { add { } remove { } }

        public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleProviderTextDeltaAsync(string textDelta, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleProviderTextDoneAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleInterruptAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RaiseSynthesisCompletedAsync() => SynthesisCompletedAsync?.Invoke() ?? Task.CompletedTask;
    }
}
