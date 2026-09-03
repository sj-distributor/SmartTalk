using Microsoft.Extensions.Configuration;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Wss.Google;
using SmartTalk.Core.Settings.Google;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The Google client appended the API key to the URI it then logged from twenty different places, so
/// any session routed to Google wrote the raw credential to Seq and to stdout at least four times —
/// at connect, connected, disconnect and dispose — where it stayed for the sink's whole retention
/// period, readable by anyone with log access and with no record of who read it.
///
/// <para>Directly at odds with the masking already done for the MiniMax key in Program.cs. Whether
/// any assistant is currently routed to Google does not change that: the leak is one configuration
/// row away, and a credential in a log has no expiry.</para>
/// </summary>
public class GoogleWssClientKeyMaskingTests
{
    private const string SentinelKey = "SENTINEL-GOOGLE-KEY-9Z8Y";

    private static GoogleRealtimeAiWssClient NewClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["Google:ApiKey"] = SentinelKey })
            .Build();

        return new GoogleRealtimeAiWssClient(new GoogleSettings(configuration));
    }

    [Fact]
    public async Task ConnectFailure_ShouldNotWriteTheApiKeyToAnyLogLine()
    {
        using var context = TestCorrelator.CreateContext();
        var sut = NewClient();

        // An unroutable host fails fast; the connect, error and cleanup lines are exactly the ones
        // that carried the credential.
        try
        {
            await sut.ConnectAsync(new Uri("wss://127.0.0.1:1/realtime"), null, CancellationToken.None);
        }
        catch
        {
            // The connection failing is the point — what it logged on the way is what matters.
        }

        var rendered = TestCorrelator.GetLogEventsFromCurrentContext()
            .Select(e => e.RenderMessage() + string.Join("|", e.Properties.Select(p => p.Value.ToString())))
            .ToList();

        rendered.ShouldNotBeEmpty("the client must have logged something for this test to mean anything");
        rendered.ShouldAllBe(text => !text.Contains(SentinelKey));
    }

    [Fact]
    public async Task PublicEndpointUri_ShouldNotCarryTheCredential()
    {
        // Also repairs a silent bug: RealtimeAiService compares WssClient.EndpointUri against the
        // configured service URL to decide whether it is already connected. With the key appended
        // that comparison could never match for Google, so the check never once did its job.
        var sut = NewClient();
        var serviceUri = new Uri("wss://127.0.0.1:1/realtime");

        try
        {
            await sut.ConnectAsync(serviceUri, null, CancellationToken.None);
        }
        catch
        {
        }

        sut.EndpointUri.ShouldNotBeNull();
        sut.EndpointUri.ToString().ShouldNotContain(SentinelKey);
        sut.EndpointUri.ShouldBe(serviceUri);
    }
}
