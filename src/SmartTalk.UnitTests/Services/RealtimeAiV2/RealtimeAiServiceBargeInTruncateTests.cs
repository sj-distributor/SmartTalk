using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// End-to-end pins for V2 user-barge-in. Drives the full
/// <c>OnAiDetectedUserSpeechAsync</c> path with item_id + clock + anchor populated
/// by upstream events and asserts the provider truncate is built with the right
/// elapsed-time math — the regression boundary for mid-greeting barge-ins where
/// OpenAI's history must stay aligned with what the user actually heard.
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
        // Cold session: user speaks before AI produced any audio → LastAssistantItemId null → skip.
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
        // Web client has no stream clock → anchor never sets → skip rather than ship a bogus offset.
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
        // After truncate the per-turn fields are cleared so a second speech-detected
        // in the same turn cannot re-truncate the already-truncated item.
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
        // Each turn's anchor re-arms after OnAiTurnCompletedAsync clears it.
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

        // Only turn 2's item_id is truncated — turn 1 completed normally so its anchor was cleared.
        ProviderAdapter.Received(1).BuildTruncateMessage("item_turn_2", Arg.Any<long>());
        ProviderAdapter.DidNotReceive().BuildTruncateMessage("item_turn_1", Arg.Any<long>());
    }
}
