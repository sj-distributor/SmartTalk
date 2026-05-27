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
/// Pins that the OpenAI adapter surfaces <c>item_id</c> on both
/// <see cref="ParsedRealtimeAiProviderEvent.ItemId"/> and (for audio deltas)
/// <see cref="RealtimeAiWssAudioData.ItemId"/> — the regression boundary for
/// barge-in: if the adapter drops <c>item_id</c>, barge-in stops working silently.
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
        // Both surfaces must carry the same id so consumers reading either path agree.
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
        // Adapter must not synthesise a value when id is absent — consumers rely on null
        // to detect "no id available".
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
        // item_id appears on transcript deltas too — must propagate regardless of event type.
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
        // response.done carries no top-level item_id; consumers must not require it on turn-end.
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
