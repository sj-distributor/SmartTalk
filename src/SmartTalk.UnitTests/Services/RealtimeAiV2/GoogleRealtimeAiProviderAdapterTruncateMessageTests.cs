using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.Google;
using SmartTalk.Core.Settings.Google;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins that the Google adapter's <c>BuildTruncateMessage</c> returns null —
/// Google's Live API handles barge-in via server-side VAD with no client-sent
/// truncate equivalent.
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
