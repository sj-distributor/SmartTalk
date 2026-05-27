using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins that the V2 OpenAI adapter surfaces the per-message <c>item_id</c> on both
/// <see cref="ParsedRealtimeAiProviderEvent.ItemId"/> and (for audio deltas)
/// <see cref="RealtimeAiWssAudioData.ItemId"/>.
///
/// <para>
/// Captured separately from the existing <c>BetaEventSunset</c> / <c>ParseMessageUsage</c>
/// suites because those assert on event type and usage; they do not pin the
/// <c>item_id</c> propagation, which is the regression boundary for the Phase 10
/// barge-in work (Phase 10.3 will read <see cref="RealtimeAiSessionContext.LastAssistantItemId"/>
/// to build the OpenAI <c>conversation.interrupt</c> message; if the adapter ever
/// drops <c>item_id</c>, barge-in stops working silently).
/// </para>
/// </summary>
public class OpenAiRealtimeAiProviderAdapterItemIdTrackingTests
{
    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    [Fact]
    public void ParseMessage_ResponseAudioDeltaWithItemId_PopulatesItemIdOnAudioData()
    {
        var raw = """
            {
              "type": "response.output_audio.delta",
              "item_id": "item_abc123",
              "delta": "AQID"
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseAudioDelta);
        var audio = parsed.Data.ShouldBeOfType<RealtimeAiWssAudioData>();
        audio.ItemId.ShouldBe("item_abc123");
        audio.Base64Payload.ShouldBe("AQID");
    }

    [Fact]
    public void ParseMessage_ResponseAudioDeltaWithItemId_AlsoSetsItemIdOnParsedEvent()
    {
        // The top-level ItemId on ParsedRealtimeAiProviderEvent must be in sync with the
        // audio-data ItemId so that consumers reading from either path see the same value.
        var raw = """
            {
              "type": "response.output_audio.delta",
              "item_id": "item_xyz789",
              "delta": "BAUG"
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.ItemId.ShouldBe("item_xyz789");
        ((RealtimeAiWssAudioData)parsed.Data).ItemId.ShouldBe(parsed.ItemId);
    }

    [Fact]
    public void ParseMessage_ResponseAudioDeltaWithoutItemId_LeavesItemIdNull()
    {
        // Defensive: providers may emit deltas without item_id (older snapshots, edge
        // events). The adapter must not synthesize a value or crash; it must leave
        // both fields as null so downstream consumers can detect "no id available".
        var raw = """
            {
              "type": "response.output_audio.delta",
              "delta": "AQID"
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseAudioDelta);
        parsed.ItemId.ShouldBeNull();
        ((RealtimeAiWssAudioData)parsed.Data).ItemId.ShouldBeNull();
    }

    [Fact]
    public void ParseMessage_NonAudioEventWithItemId_PopulatesParsedEventItemId()
    {
        // item_id appears on non-audio events too (e.g. response.output_audio_transcript.delta).
        // It must propagate to ParsedRealtimeAiProviderEvent.ItemId regardless of event type
        // so Phase 10 can correlate transcripts to specific assistant items if needed.
        var raw = """
            {
              "type": "response.output_audio_transcript.delta",
              "item_id": "item_transcript_1",
              "delta": "hello"
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.OutputAudioTranscriptionPartial);
        parsed.ItemId.ShouldBe("item_transcript_1");
    }

    [Fact]
    public void ParseMessage_TurnCompletedEvent_ItemIdNotRequired()
    {
        // response.done events don't carry item_id at the top level; consumers must not
        // rely on it being present for turn completion. This pins the contract so a
        // future barge-in / cleanup refactor that incorrectly required ItemId on turn-end
        // would surface here.
        var raw = """
            {
              "type": "response.done",
              "response": { "id": "resp_test" }
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseTurnCompleted);
        parsed.ItemId.ShouldBeNull();
    }
}
