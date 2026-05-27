using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// End-to-end pins for Phase 10.3 V2 barge-in. Exercises the full
/// <c>OnAiDetectedUserSpeechAsync</c> path with item_id (Phase 10.1),
/// LatestMediaTimestamp + ResponseStartTimestampTwilio (Phase 10.2)
/// already populated by upstream events, and asserts the provider
/// <c>BuildTruncateMessage</c> is called with the elapsed-time math
/// — the regression boundary for restaurant calls where users barge
/// in mid-greeting and OpenAI's conversation history must stay aligned
/// with what the user actually heard.
/// </summary>
public class RealtimeAiServiceBargeInTruncateTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task SpeechDetected_AfterAiSpokeWithItemIdAndTimestamps_SendsTruncate()
    {
        const string itemId = "item_barge_in";
        const long clientStartTimestamp = 1000;
        const long clientEndTimestamp = 1750;
        const long expectedElapsedMs = 750;

        var providerCall = 0;
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(_ =>
            {
                providerCall++;
                return providerCall == 1
                    ? new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.ResponseAudioDelta,
                        Data = new RealtimeAiWssAudioData { Base64Payload = "AQID", ItemId = itemId }
                    }
                    : new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SpeechDetected };
            });

        var clientCall = 0;
        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(_ =>
            {
                clientCall++;
                return new ParsedClientMessage
                {
                    Type = RealtimeAiClientMessageType.Audio,
                    Payload = "AQID",
                    Timestamp = clientCall == 1 ? clientStartTimestamp : clientEndTimestamp
                };
            });

        ProviderAdapter.BuildTruncateMessage(Arg.Any<string>(), Arg.Any<long>()).Returns("{}");

        var sessionTask = await StartSessionInBackgroundAsync();

        // 1. User audio @ ts=1000 → LatestMediaTimestamp = 1000
        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"x\",\"timestamp\":\"1000\"}}");
        await Task.Delay(50);

        // 2. AI audio delta with item_id → LastAssistantItemId set, anchor = 1000
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.output_audio.delta\"}");
        await Task.Delay(50);

        // 3. User audio @ ts=1750 → LatestMediaTimestamp = 1750
        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"y\",\"timestamp\":\"1750\"}}");
        await Task.Delay(50);

        // 4. SpeechDetected → barge-in: BuildTruncateMessage(itemId, 1750-1000=750)
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"input_audio_buffer.speech_started\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        ProviderAdapter.Received(1).BuildTruncateMessage(itemId, expectedElapsedMs);
    }

    [Fact]
    public async Task SpeechDetected_WithoutPriorAiAudio_DoesNotSendTruncate()
    {
        // Cold-session edge case: user starts speaking before AI has produced any
        // audio. LastAssistantItemId stays null; truncate must be skipped silently.
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SpeechDetected });

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = "AQID", Timestamp = 1000 });

        var sessionTask = await StartSessionInBackgroundAsync();

        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"x\",\"timestamp\":\"1000\"}}");
        await Task.Delay(50);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"input_audio_buffer.speech_started\"}");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask;

        ProviderAdapter.DidNotReceive().BuildTruncateMessage(Arg.Any<string>(), Arg.Any<long>());
    }

    [Fact]
    public async Task SpeechDetected_AiSpokeButNoClientTimestamp_DoesNotSendTruncate()
    {
        // Web (DefaultRealtimeAiClientAdapter) does not surface a stream clock. Without
        // LatestMediaTimestamp the per-turn anchor never gets set, so the elapsed-time
        // math has no input — skip the truncate rather than ship a bogus offset.
        var providerCall = 0;
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(_ =>
            {
                providerCall++;
                return providerCall == 1
                    ? new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.ResponseAudioDelta,
                        Data = new RealtimeAiWssAudioData { Base64Payload = "AQID", ItemId = "item_test" }
                    }
                    : new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SpeechDetected };
            });

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = "AQID" /* no Timestamp */ });

        var sessionTask = await StartSessionInBackgroundAsync();

        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"x\"}}");
        await Task.Delay(50);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.output_audio.delta\"}");
        await Task.Delay(50);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"input_audio_buffer.speech_started\"}");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask;

        ProviderAdapter.DidNotReceive().BuildTruncateMessage(Arg.Any<string>(), Arg.Any<long>());
    }

    [Fact]
    public async Task SpeechDetectedTwiceInARow_OnlyTruncatesOnce()
    {
        // After a successful truncate the per-turn fields are cleared so a second
        // speech-detected within the same turn can't re-truncate the now-already-truncated
        // item (OpenAI would error). Pin this so a future refactor of the cleanup
        // doesn't reintroduce double-truncate.
        const string itemId = "item_once";

        var providerCall = 0;
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(_ =>
            {
                providerCall++;
                return providerCall == 1
                    ? new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.ResponseAudioDelta,
                        Data = new RealtimeAiWssAudioData { Base64Payload = "AQID", ItemId = itemId }
                    }
                    : new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SpeechDetected };
            });

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = "AQID", Timestamp = 500 });

        ProviderAdapter.BuildTruncateMessage(Arg.Any<string>(), Arg.Any<long>()).Returns("{}");

        var sessionTask = await StartSessionInBackgroundAsync();

        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"x\",\"timestamp\":\"500\"}}");
        await Task.Delay(50);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.output_audio.delta\"}");
        await Task.Delay(50);

        // First barge-in
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"input_audio_buffer.speech_started\"}");
        await Task.Delay(50);

        // Second barge-in (no new AI delta in between)
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"input_audio_buffer.speech_started\"}");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask;

        ProviderAdapter.Received(1).BuildTruncateMessage(itemId, Arg.Any<long>());
    }

    [Fact]
    public async Task SpeechDetected_AcrossTurns_TruncatesEachTurnIndependently()
    {
        // Two complete turns of AI speech, each interrupted. Both must emit a truncate
        // with their own item_id and elapsed time — confirms the per-turn anchor
        // re-arms after OnAiTurnCompletedAsync clears it.
        var providerCall = 0;
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(_ =>
            {
                providerCall++;
                return providerCall switch
                {
                    1 => new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.ResponseAudioDelta,
                        Data = new RealtimeAiWssAudioData { Base64Payload = "AQID", ItemId = "item_turn_1" }
                    },
                    2 => new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                        Data = new List<RealtimeAiWssFunctionCallData>()
                    },
                    3 => new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.ResponseAudioDelta,
                        Data = new RealtimeAiWssAudioData { Base64Payload = "AQID", ItemId = "item_turn_2" }
                    },
                    _ => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SpeechDetected }
                };
            });

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = "AQID", Timestamp = 100 });

        ProviderAdapter.BuildTruncateMessage(Arg.Any<string>(), Arg.Any<long>()).Returns("{}");

        var sessionTask = await StartSessionInBackgroundAsync();

        // Clock established
        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"x\",\"timestamp\":\"100\"}}");
        await Task.Delay(50);

        // Turn 1: AI delta + completed
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.output_audio.delta\"}");
        await Task.Delay(50);
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(50);

        // Turn 2: AI delta then barge-in
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.output_audio.delta\"}");
        await Task.Delay(50);
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"input_audio_buffer.speech_started\"}");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Only turn 2's item_id should be truncated — turn 1 was completed normally
        // and its anchor was cleared by OnAiTurnCompletedAsync.
        ProviderAdapter.Received(1).BuildTruncateMessage("item_turn_2", Arg.Any<long>());
        ProviderAdapter.DidNotReceive().BuildTruncateMessage("item_turn_1", Arg.Any<long>());
    }
}
