using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Clients.Twilio;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins that the Twilio adapter surfaces <c>media.timestamp</c> on
/// <see cref="ParsedClientMessage.Timestamp"/> — the input to the barge-in elapsed-time
/// calculation. Losing this field would silently un-truncate OpenAI's conversation
/// history.
/// </summary>
public class TwilioRealtimeAiClientAdapterMediaTimestampTests
{
    private static TwilioRealtimeAiClientAdapter NewAdapter() => new();

    [Fact]
    public void ParseMessage_MediaWithStringTimestamp_PopulatesTimestamp()
    {
        // Canonical Twilio shape: timestamp is a string of ms since stream start.
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
        // Numeric form occasionally appears in real payloads — accept it as well as the doc'd string form.
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
        // Adapter must not synthesise a timestamp when absent — would break elapsed math.
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
        // Malformed timestamp must not drop the audio frame — payload is delivered, only Timestamp is null.
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
        // Lifecycle events have no timestamp — must stay null so they don't clobber the running clock.
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
        // Video frames advance the stream clock too — different handler, same parser surface.
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
