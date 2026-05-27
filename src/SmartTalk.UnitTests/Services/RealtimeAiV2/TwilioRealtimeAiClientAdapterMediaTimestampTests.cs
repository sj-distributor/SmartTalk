using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Clients.Twilio;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins that the Twilio client adapter surfaces <c>media.timestamp</c> on
/// <see cref="SmartTalk.Core.Services.RealtimeAiV2.Adapters.ParsedClientMessage.Timestamp"/>.
///
/// <para>
/// Phase 10.3 will use the running <c>_ctx.LatestMediaTimestamp</c> minus the per-turn
/// <c>_ctx.ResponseStartTimestampTwilio</c> snapshot to compute <c>audio_end_ms</c> for
/// the OpenAI <c>conversation.item.truncate</c> sent at user barge-in time. If the
/// adapter ever stopped extracting <c>media.timestamp</c>, barge-in would still fire
/// (the Twilio <c>clear</c> event already stops playback), but the conversation
/// history sent to OpenAI for the next turn would carry the un-truncated assistant
/// utterance and confuse subsequent prompt context.
/// </para>
/// </summary>
public class TwilioRealtimeAiClientAdapterMediaTimestampTests
{
    private static TwilioRealtimeAiClientAdapter NewAdapter() => new();

    [Fact]
    public void ParseMessage_MediaWithStringTimestamp_PopulatesTimestamp()
    {
        // Canonical Twilio shape per Media Streams docs: timestamp is a string of
        // milliseconds since the stream started.
        const string raw = """
            {
              "event": "media",
              "streamSid": "MZ-test",
              "media": { "payload": "AQID", "timestamp": "1234" }
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiClientMessageType.Audio);
        parsed.Timestamp.ShouldBe(1234L);
    }

    [Fact]
    public void ParseMessage_MediaWithNumericTimestamp_PopulatesTimestamp()
    {
        // Real-world payloads occasionally surface the numeric form (older preview
        // snapshots, future protocol revisions). Accept both rather than silently
        // dropping one variant — the audio payload is too critical to lose.
        const string raw = """
            {
              "event": "media",
              "streamSid": "MZ-test",
              "media": { "payload": "AQID", "timestamp": 4567 }
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiClientMessageType.Audio);
        parsed.Timestamp.ShouldBe(4567L);
    }

    [Fact]
    public void ParseMessage_MediaWithoutTimestamp_LeavesTimestampNull()
    {
        // Some Twilio snapshots / connect-flow setups don't include timestamp.
        // The adapter must not synthesise one (would break Phase 10.3 elapsed math).
        const string raw = """
            {
              "event": "media",
              "streamSid": "MZ-test",
              "media": { "payload": "AQID" }
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiClientMessageType.Audio);
        parsed.Timestamp.ShouldBeNull();
    }

    [Fact]
    public void ParseMessage_MediaWithNonNumericStringTimestamp_LeavesTimestampNull()
    {
        // Malformed timestamp string must not drop the audio frame. The payload is
        // delivered (Type = Audio, Payload is populated); only Timestamp is null.
        const string raw = """
            {
              "event": "media",
              "streamSid": "MZ-test",
              "media": { "payload": "AQID", "timestamp": "not-a-number" }
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiClientMessageType.Audio);
        parsed.Payload.ShouldBe("AQID");
        parsed.Timestamp.ShouldBeNull();
    }

    [Fact]
    public void ParseMessage_StartEvent_LeavesTimestampNull()
    {
        // Lifecycle events don't carry timestamp; the field must stay null so the
        // service doesn't accidentally clobber LatestMediaTimestamp with zero between
        // genuine media frames.
        const string raw = """
            {
              "event": "start",
              "start": { "streamSid": "MZ-test", "callSid": "CA-test" }
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiClientMessageType.Start);
        parsed.Timestamp.ShouldBeNull();
    }

    [Fact]
    public void ParseMessage_VideoMediaWithTimestamp_PopulatesTimestamp()
    {
        // Image frames also advance the stream clock. They go to a different handler
        // but must still update LatestMediaTimestamp via the parser surface.
        const string raw = """
            {
              "event": "media",
              "streamSid": "MZ-test",
              "media": { "payload": "AQID", "type": "video", "timestamp": "9999" }
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiClientMessageType.Image);
        parsed.Timestamp.ShouldBe(9999L);
    }
}
