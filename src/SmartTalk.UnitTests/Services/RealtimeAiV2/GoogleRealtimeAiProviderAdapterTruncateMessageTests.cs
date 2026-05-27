using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.Google;
using SmartTalk.Core.Settings.Google;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins that the Google adapter's <c>BuildTruncateMessage</c> returns null. Google's
/// Live API relies on its server-side VAD to handle barge-in; sending a client-side
/// truncate would either be ignored or generate a protocol-error log on Google's side.
/// The caller (<c>RealtimeAiService.SendBargeInTruncateIfApplicableAsync</c>) checks
/// for null and skips <c>SendToProviderAsync</c>, while the Twilio <c>clear</c> frame
/// still flushes the audio buffer at the client side.
/// </summary>
public class GoogleRealtimeAiProviderAdapterTruncateMessageTests
{
    private static GoogleRealtimeAiProviderAdapter NewAdapter() =>
        new(new GoogleSettings(Substitute.For<IConfiguration>()));

    [Fact]
    public void BuildTruncateMessage_ValidInput_ReturnsNull()
    {
        NewAdapter().BuildTruncateMessage("item_abc", 1500).ShouldBeNull();
    }

    [Fact]
    public void BuildTruncateMessage_NullInput_ReturnsNull()
    {
        NewAdapter().BuildTruncateMessage(null, 0).ShouldBeNull();
    }
}
