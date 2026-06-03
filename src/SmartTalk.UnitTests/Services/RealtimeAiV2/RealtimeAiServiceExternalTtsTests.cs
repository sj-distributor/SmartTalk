using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class RealtimeAiServiceExternalTtsTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task ProviderTextOutput_IsSynthesizedByExternalTtsAndSentToClient()
    {
        var externalTts = new FakeExternalTtsProvider();
        Switcher.TtsProvider(RealtimeAiTtsProviderType.MiniMax).Returns(externalTts);

        ProviderAdapter.ParseMessage("text-delta").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTextDelta,
            Data = new RealtimeAiWssTextData { Text = "hello " }
        });
        ProviderAdapter.ParseMessage("text-done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTextDone,
            Data = new RealtimeAiWssTextData { Text = "world" }
        });

        var options = CreateDefaultOptions(x =>
        {
            x.TtsConfig = new RealtimeAiTtsConfig
            {
                ProviderType = RealtimeAiTtsProviderType.MiniMax,
                SampleRate = 24000
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("text-delta");
        await FakeWssClient.SimulateMessageReceivedAsync("text-done");

        externalTts.TextDeltas.ShouldBe(new[] { "hello " });
        externalTts.TextDoneCount.ShouldBe(1);
        FakeWs.GetSentTextMessages().ShouldContain(x => x.Contains("AQID"));

        FakeWs.EnqueueClose();
        await sessionTask;
    }

    [Fact]
    public async Task ProviderTextDone_WithNoPriorDelta_SynthesizesCompletedText()
    {
        var externalTts = new FakeExternalTtsProvider();
        Switcher.TtsProvider(RealtimeAiTtsProviderType.MiniMax).Returns(externalTts);

        ProviderAdapter.ParseMessage("text-done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTextDone,
            Data = new RealtimeAiWssTextData { Text = "hello from done" }
        });

        var sessionTask = await StartSessionInBackgroundAsync(CreateExternalTtsOptions());

        await FakeWssClient.SimulateMessageReceivedAsync("text-done");

        externalTts.TextDeltas.ShouldBe(new[] { "hello from done" });
        externalTts.TextDoneCount.ShouldBe(1);
        FakeWs.GetSentTextMessages().ShouldContain(x => x.Contains("AQID"));

        FakeWs.EnqueueClose();
        await sessionTask;
    }

    [Fact]
    public async Task ResponseDone_WithCompletedTextOnly_SynthesizesCompletedText()
    {
        var externalTts = new FakeExternalTtsProvider();
        Switcher.TtsProvider(RealtimeAiTtsProviderType.MiniMax).Returns(externalTts);

        ProviderAdapter.ParseMessage("response-done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTurnCompleted,
            Data = new RealtimeAiWssTextData { Text = "hello from response done" }
        });

        var sessionTask = await StartSessionInBackgroundAsync(CreateExternalTtsOptions());

        await FakeWssClient.SimulateMessageReceivedAsync("response-done");

        externalTts.TextDeltas.ShouldBe(new[] { "hello from response done" });
        externalTts.TextDoneCount.ShouldBe(1);
        FakeWs.GetSentTextMessages().ShouldContain(x => x.Contains("AQID"));

        FakeWs.EnqueueClose();
        await sessionTask;
    }

    [Fact]
    public async Task ExternalTts_TurnCompletesOnlyAfterProviderDoneAndTtsDone()
    {
        var externalTts = new FakeExternalTtsProvider(autoComplete: false);
        Switcher.TtsProvider(RealtimeAiTtsProviderType.MiniMax).Returns(externalTts);

        ProviderAdapter.ParseMessage("text-delta").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTextDelta,
            Data = new RealtimeAiWssTextData { Text = "hello" }
        });
        ProviderAdapter.ParseMessage("response-done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTurnCompleted
        });

        var sessionTask = await StartSessionInBackgroundAsync(CreateExternalTtsOptions());

        await FakeWssClient.SimulateMessageReceivedAsync("text-delta");
        await FakeWssClient.SimulateMessageReceivedAsync("response-done");

        FakeWs.GetSentTextMessages().ShouldNotContain(x => x.Contains("AiTurnCompleted"));

        await externalTts.CompleteAsync();

        FakeWs.GetSentTextMessages().ShouldContain(x => x.Contains("AiTurnCompleted"));

        FakeWs.EnqueueClose();
        await sessionTask;
    }

    [Fact]
    public async Task BargeIn_WithExternalTts_DoesNotTruncateProviderButInterruptsTts()
    {
        var externalTts = new FakeExternalTtsProvider();
        Switcher.TtsProvider(RealtimeAiTtsProviderType.MiniMax).Returns(externalTts);

        ProviderAdapter.ParseMessage("text-delta").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTextDelta,
            ItemId = "item_external_tts",
            Data = new RealtimeAiWssTextData { Text = "hello" }
        });
        ProviderAdapter.ParseMessage("text-done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTextDone,
            ItemId = "item_external_tts",
            Data = new RealtimeAiWssTextData { Text = "hello" }
        });
        ProviderAdapter.ParseMessage("speech").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.SpeechDetected
        });

        var clientCall = 0;
        ClientAdapter.ParseMessage(Arg.Any<string>()).Returns(_ => new ParsedClientMessage
        {
            Type = RealtimeAiClientMessageType.Audio,
            Payload = "AQID",
            Timestamp = ++clientCall == 1 ? 1000 : 1500
        });

        var sessionTask = await StartSessionInBackgroundAsync(CreateExternalTtsOptions());

        // Establish stream clock, then a text turn whose synthesized audio sets the barge-in anchor.
        FakeWs.EnqueueClientMessage("{\"media\":{\"timestamp\":\"1000\"}}");
        await Task.Delay(50);
        await FakeWssClient.SimulateMessageReceivedAsync("text-delta");
        await FakeWssClient.SimulateMessageReceivedAsync("text-done");
        await Task.Delay(50);
        FakeWs.EnqueueClientMessage("{\"media\":{\"timestamp\":\"1500\"}}");
        await Task.Delay(50);

        // User barges in: item_id + clock + anchor are all set, but text-only provider has no audio
        // to truncate, so we must skip the provider truncate and instead interrupt the TTS provider.
        await FakeWssClient.SimulateMessageReceivedAsync("speech");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask;

        ProviderAdapter.DidNotReceive().BuildTruncateMessage(Arg.Any<string>(), Arg.Any<long>());
        externalTts.InterruptCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExternalTts_AssistantText_IsCapturedInTranscriptions()
    {
        var externalTts = new FakeExternalTtsProvider();
        Switcher.TtsProvider(RealtimeAiTtsProviderType.MiniMax).Returns(externalTts);

        ProviderAdapter.ParseMessage("text-delta").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTextDelta,
            Data = new RealtimeAiWssTextData { Text = "Hello" }
        });
        ProviderAdapter.ParseMessage("text-done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTextDone,
            Data = new RealtimeAiWssTextData { Text = "Hello there" }
        });

        List<(AiSpeechAssistantSpeaker Speaker, string Text)>? captured = null;
        var options = CreateExternalTtsOptions();
        options.OnTranscriptionsCompletedAsync = (_, transcriptions) =>
        {
            captured = transcriptions.ToList();
            return Task.CompletedTask;
        };

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("text-delta");
        await FakeWssClient.SimulateMessageReceivedAsync("text-done");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask;

        captured.ShouldNotBeNull();
        captured.ShouldContain(t => t.Speaker == AiSpeechAssistantSpeaker.Ai && t.Text == "Hello there");
        ClientAdapter.Received().BuildTranscriptionMessage(
            RealtimeAiWssEventType.OutputAudioTranscriptionCompleted,
            Arg.Is<RealtimeAiWssTranscriptionData>(d => d.Speaker == AiSpeechAssistantSpeaker.Ai && d.Transcript == "Hello there"),
            Arg.Any<string>());
    }

    private RealtimeSessionOptions CreateExternalTtsOptions()
    {
        return CreateDefaultOptions(x =>
        {
            x.TtsConfig = new RealtimeAiTtsConfig
            {
                ProviderType = RealtimeAiTtsProviderType.MiniMax,
                SampleRate = 24000
            };
        });
    }

    private sealed class FakeExternalTtsProvider : IRealtimeAiTtsProvider
    {
        private readonly bool _autoComplete;

        public FakeExternalTtsProvider(bool autoComplete = true)
        {
            _autoComplete = autoComplete;
        }

        public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.MiniMax;

        public RealtimeAiAudioCodec OutputCodec => RealtimeAiAudioCodec.PCM16;

        public int OutputSampleRate { get; private set; } = 24000;

        public List<string> TextDeltas { get; } = new();

        public int TextDoneCount { get; private set; }

        public int InterruptCount { get; private set; }

        public event Func<string, Task>? AudioChunkReadyAsync;

        public event Func<Task>? SynthesisCompletedAsync;

        public event Func<RealtimeAiErrorData, Task>? SynthesisFailedAsync
        {
            add { }
            remove { }
        }

        public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken)
        {
            OutputSampleRate = config.SampleRate ?? 24000;
            return Task.CompletedTask;
        }

        public Task HandleProviderAudioAsync(string base64Audio, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task HandleProviderTextDeltaAsync(string textDelta, CancellationToken cancellationToken)
        {
            TextDeltas.Add(textDelta);
            return Task.CompletedTask;
        }

        public async Task HandleProviderTextDoneAsync(CancellationToken cancellationToken)
        {
            TextDoneCount += 1;
            await (AudioChunkReadyAsync?.Invoke("AQID") ?? Task.CompletedTask).ConfigureAwait(false);
            if (_autoComplete)
                await CompleteAsync().ConfigureAwait(false);
        }

        public async Task CompleteAsync()
        {
            await (SynthesisCompletedAsync?.Invoke() ?? Task.CompletedTask).ConfigureAwait(false);
        }

        public Task HandleInterruptAsync(CancellationToken cancellationToken)
        {
            InterruptCount += 1;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
