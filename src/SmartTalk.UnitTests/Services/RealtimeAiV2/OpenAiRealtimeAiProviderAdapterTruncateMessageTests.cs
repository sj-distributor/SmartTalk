using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins the GA <c>conversation.item.truncate</c> shape built by the OpenAI adapter
/// at user barge-in. Wrong payload → OpenAI rejects → next turn responds as if the
/// user heard the full AI utterance (subtly wrong restaurant-call context).
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
        // OpenAI accepts 0; the field must still be present (don't treat 0 as "default and omit").
        var json = NewAdapter().BuildTruncateMessage("item_abc", 0);

        var parsed = JObject.Parse(json!);
        parsed["audio_end_ms"]!.Value<long>().ShouldBe(0);
    }

    [Fact]
    public void BuildTruncateMessage_NegativeAudioEndMs_ClampsToZero()
    {
        // Adapter clamps even though the caller already does — defensive against future consumers.
        var json = NewAdapter().BuildTruncateMessage("item_abc", -42);

        var parsed = JObject.Parse(json!);
        parsed["audio_end_ms"]!.Value<long>().ShouldBe(0);
    }

    [Fact]
    public void BuildTruncateMessage_NullItemId_ReturnsNull()
    {
        NewAdapter().BuildTruncateMessage(null, 100).ShouldBeNull();
    }

    [Fact]
    public void BuildTruncateMessage_EmptyItemId_ReturnsNull()
    {
        NewAdapter().BuildTruncateMessage("", 100).ShouldBeNull();
    }

    [Fact]
    public void BuildTruncateMessage_LargeAudioEndMs_RoundTripsExact()
    {
        // Long calls can exceed int range — pin the long path to keep precision.
        var json = NewAdapter().BuildTruncateMessage("item_long", 9_000_000_000L);

        var parsed = JObject.Parse(json!);
        parsed["audio_end_ms"]!.Value<long>().ShouldBe(9_000_000_000L);
    }
}
