using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins the GA <c>conversation.item.truncate</c> shape built by the OpenAI V2 adapter
/// for Phase 10.3 barge-in. If OpenAI ever rejects this payload, the Twilio <c>clear</c>
/// frame still stops playback, but the assistant message in OpenAI's history reverts
/// to the un-truncated form and the next turn responds as if the user heard the AI
/// finish (subtly wrong restaurant-call context).
/// </summary>
public class OpenAiRealtimeAiProviderAdapterTruncateMessageTests
{
    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    [Fact]
    public void BuildTruncateMessage_ValidInput_EmitsCanonicalGaShape()
    {
        var json = NewAdapter().BuildTruncateMessage("item_abc123", 1500);

        json.ShouldNotBeNull();
        var parsed = JObject.Parse(json);
        parsed["type"]!.Value<string>().ShouldBe("conversation.item.truncate");
        parsed["item_id"]!.Value<string>().ShouldBe("item_abc123");
        parsed["content_index"]!.Value<int>().ShouldBe(0);
        parsed["audio_end_ms"]!.Value<long>().ShouldBe(1500);
    }

    [Fact]
    public void BuildTruncateMessage_ZeroAudioEndMs_StillEmitsZeroNotOmits()
    {
        // OpenAI accepts audio_end_ms = 0 (means "user interrupted before any audio
        // played"); the field must still be present in the payload. A common refactor
        // mistake is to treat 0 as "default" and omit the key, which OpenAI would
        // reject as a missing required field.
        var json = NewAdapter().BuildTruncateMessage("item_abc", 0);

        var parsed = JObject.Parse(json!);
        parsed["audio_end_ms"]!.Value<long>().ShouldBe(0);
    }

    [Fact]
    public void BuildTruncateMessage_NegativeAudioEndMs_ClampsToZero()
    {
        // Service-side caller already clamps, but the adapter guards too — a faulty
        // future consumer that bypassed the clamp must not ship a payload OpenAI
        // would reject.
        var json = NewAdapter().BuildTruncateMessage("item_abc", -42);

        var parsed = JObject.Parse(json!);
        parsed["audio_end_ms"]!.Value<long>().ShouldBe(0);
    }

    [Fact]
    public void BuildTruncateMessage_NullItemId_ReturnsNull()
    {
        // Sending the event without item_id would be rejected by the GA server.
        // Returning null lets the caller skip SendToProviderAsync without a warning
        // (the per-turn anchor may also be null in this case, e.g. cold session).
        NewAdapter().BuildTruncateMessage(null, 100).ShouldBeNull();
    }

    [Fact]
    public void BuildTruncateMessage_EmptyItemId_ReturnsNull()
    {
        // Empty string is treated identically to null — both indicate "no in-flight
        // assistant turn to truncate". The skip is silent to avoid noise on every
        // user-speech-detected event during cold-session warm-up.
        NewAdapter().BuildTruncateMessage("", 100).ShouldBeNull();
    }

    [Fact]
    public void BuildTruncateMessage_LargeAudioEndMs_RoundTripsExact()
    {
        // Long-running calls (hour-plus restaurant queues) can push audio_end_ms beyond
        // int range. Pin that the long path holds without precision loss.
        var json = NewAdapter().BuildTruncateMessage("item_long", 9_000_000_000L);

        var parsed = JObject.Parse(json!);
        parsed["audio_end_ms"]!.Value<long>().ShouldBe(9_000_000_000L);
    }
}
