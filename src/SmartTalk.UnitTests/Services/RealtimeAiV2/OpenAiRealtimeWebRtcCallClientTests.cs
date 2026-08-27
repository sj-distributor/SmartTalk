using System.Net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.RealtimeAiWebRtc;
using SmartTalk.Core.Settings.OpenAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class OpenAiRealtimeWebRtcCallClientTests
{
    [Fact]
    public async Task HangupCallAsync_PostsToCallHangupEndpoint()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<ISmartTalkHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<Dictionary<string, string>>())
            .Returns(httpClient);
        var settings = new OpenAiSettings(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAi:ApiKey"] = "test-key"
            })
            .Build());
        var client = new OpenAiRealtimeWebRtcCallClient(settings, httpClientFactory);

        await client.HangupCallAsync(
            "rtc_test_123",
            "wss://api.openai.com/v1/realtime?model=gpt-realtime-test",
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://api.openai.com/v1/realtime/calls/rtc_test_123/hangup",
            handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-key", handler.AuthorizationParameter);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
