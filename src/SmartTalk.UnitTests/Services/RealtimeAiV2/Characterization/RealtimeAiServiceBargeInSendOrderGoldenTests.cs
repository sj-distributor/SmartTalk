using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the barge-in SEND ORDER in OnAiDetectedUserSpeechAsync:
/// client "clear" (BuildSpeechDetectedMessage, the time-critical playback stop) → provider truncate
/// (BuildTruncateMessage, history correction) → TTS interrupt (HandleInterruptAsync).
///
/// The existing BargeInTruncate tests assert only the truncate call COUNT/args. The three sends land
/// in three different sinks (client socket, provider socket, TTS provider), so no existing test can
/// prove their relative order — a reorder that delays the client clear ships green today. This test
/// records all three on one shared sequence (captured at the send-adjacent call site, which is
/// sequential in the production method). Migration steps S6 (interrupt placement) and S9–S12
/// (turn-lock/barge-in interplay) must preserve this order, or it fails RED.
/// </summary>
public class RealtimeAiServiceBargeInSendOrderGoldenTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task BargeIn_OrdersClientClearThenProviderTruncateThenTtsInterrupt()
    {
        var order = new List<string>();
        var gate = new object();
        void Record(string channel) { lock (gate) order.Add(channel); }

        // Recording TTS mirrors BuiltIn passthrough (forwards provider audio) and records the interrupt.
        var recordingTts = new OrderRecordingTtsProvider(() => Record("tts-interrupt"));
        Switcher.TtsProvider(Arg.Any<RealtimeAiTtsProviderType>()).Returns(recordingTts);

        // Record the client-clear and provider-truncate at their (sequential, send-adjacent) build sites.
        ClientAdapter.BuildSpeechDetectedMessage(Arg.Any<string>())
            .Returns(_ => { Record("client-clear"); return new { type = "SpeechDetected" }; });
        ProviderAdapter.BuildTruncateMessage(Arg.Any<string>(), Arg.Any<long>())
            .Returns(_ => { Record("provider-truncate"); return "truncate_msg"; });

        // Client media frame carries the stream clock; provider events drive audio + barge-in.
        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = "AAAA", Timestamp = 1000 });
        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0) switch
        {
            "audio" => new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseAudioDelta,
                Data = new RealtimeAiWssAudioData { ItemId = "item_1", Base64Payload = "AAAA" }
            },
            "speech" => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SpeechDetected },
            _ => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Unknown }
        });

        var sessionTask = await StartSessionInBackgroundAsync();

        // 1) media frame sets the stream clock (LatestMediaTimestamp).
        FakeWs.EnqueueClientMessage("media");
        await Task.Delay(50);

        // 2) AI audio delta sets the assistant item id + the per-turn anchor (= current clock).
        await FakeWssClient.SimulateMessageReceivedAsync("audio");
        await Task.Delay(20);

        // 3) user barge-in → clear, truncate, interrupt.
        await FakeWssClient.SimulateMessageReceivedAsync("speech");
        await Task.Delay(20);

        FakeWs.EnqueueClose();
        await sessionTask;

        order.ShouldBe(new[] { "client-clear", "provider-truncate", "tts-interrupt" });
    }

    /// <summary>
    /// Mirrors <c>BuiltInRealtimeAiTtsProvider</c> (audio passthrough, BuiltIn type so the engine takes
    /// the truncate path) and records when the engine calls HandleInterruptAsync.
    /// </summary>
    private sealed class OrderRecordingTtsProvider : IRealtimeAiTtsProvider, IRealtimeAiAudioPassthrough
    {
        private readonly Action _onInterrupt;

        public OrderRecordingTtsProvider(Action onInterrupt) => _onInterrupt = onInterrupt;

        public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.BuiltIn;
        public RealtimeAiAudioCodec OutputCodec { get; private set; } = RealtimeAiAudioCodec.PCM16;
        public int OutputSampleRate { get; private set; } = 24000;

        public event Func<string, Task>? AudioChunkReadyAsync;
        public event Func<Task>? SynthesisCompletedAsync;
        public event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync { add { } remove { } }

        public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken)
        {
            OutputCodec = config.TargetCodec;
            OutputSampleRate = config.SampleRate ?? AudioCodecConverter.GetSampleRate(OutputCodec);
            return Task.CompletedTask;
        }

        public Task HandleProviderAudioAsync(string base64Audio, CancellationToken cancellationToken) =>
            AudioChunkReadyAsync?.Invoke(base64Audio) ?? Task.CompletedTask;

        public Task HandleProviderAudioDoneAsync(CancellationToken cancellationToken) =>
            SynthesisCompletedAsync?.Invoke() ?? Task.CompletedTask;

        public Task HandleInterruptAsync(CancellationToken cancellationToken)
        {
            _onInterrupt();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
