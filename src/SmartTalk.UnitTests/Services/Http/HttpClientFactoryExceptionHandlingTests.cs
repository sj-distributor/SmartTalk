using System.Net;
using System.Net.Http.Headers;
using Autofac;
using Shouldly;
using SmartTalk.Core.Services.Http;
using Xunit;

namespace SmartTalk.UnitTests.Services.Http;

public class HttpClientFactoryExceptionHandlingTests
{
    [Fact]
    public async Task SmartTalkFactory_should_rethrow_transport_exception()
    {
        using var container = CreateContainer((_, _) => throw new InvalidOperationException("boom"));
        var sut = new SmartTalkHttpClientFactory(container);

        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await sut.GetAsync<string>("https://example.com/test", CancellationToken.None).ConfigureAwait(false));

        ex.Message.ShouldBe("boom");
    }

    [Fact]
    public async Task SmartTalkFactory_should_throw_http_request_exception_for_non_success_response()
    {
        using var container = CreateContainer((_, _) => Task.FromResult(CreateResponse(HttpStatusCode.BadRequest, "bad request body")));
        var sut = new SmartTalkHttpClientFactory(container);

        var ex = await Should.ThrowAsync<HttpRequestException>(async () =>
            await sut.GetAsync<string>("https://example.com/test", CancellationToken.None).ConfigureAwait(false));

        ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ex.Message.ShouldContain("HTTP request failed.");
        ex.Message.ShouldContain("StatusCode=400 (BadRequest)");
        ex.Message.ShouldContain("Body=bad request body");
    }

    [Fact]
    public async Task SmartTalkFactory_should_preserve_inner_exception_when_response_deserialization_fails()
    {
        using var container = CreateContainer((_, _) => Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, "{invalid-json}")));
        var sut = new SmartTalkHttpClientFactory(container);

        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await sut.GetAsync<TestResponse>("https://example.com/test", CancellationToken.None).ConfigureAwait(false));

        ex.Message.ShouldContain("Failed to deserialize HTTP response.");
        ex.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public async Task SmartiesFactory_should_rethrow_transport_exception()
    {
        using var container = CreateContainer((_, _) => throw new InvalidOperationException("smarties-boom"));
        var sut = new SmartiesHttpClientFactory(container);

        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await sut.GetAsync<string>("https://example.com/test", CancellationToken.None).ConfigureAwait(false));

        ex.Message.ShouldBe("smarties-boom");
    }

    [Fact]
    public async Task SmartiesFactory_should_throw_http_request_exception_for_non_success_response()
    {
        using var container = CreateContainer((_, _) => Task.FromResult(CreateResponse(HttpStatusCode.BadGateway, "upstream failed")));
        var sut = new SmartiesHttpClientFactory(container);

        var ex = await Should.ThrowAsync<HttpRequestException>(async () =>
            await sut.GetAsync<string>("https://example.com/test", CancellationToken.None).ConfigureAwait(false));

        ex.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        ex.Message.ShouldContain("HTTP request failed.");
        ex.Message.ShouldContain("StatusCode=502 (BadGateway)");
        ex.Message.ShouldContain("Body=upstream failed");
    }

    private static IContainer CreateContainer(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
        var builder = new ContainerBuilder();
        var client = new HttpClient(new DelegateHttpMessageHandler(responseFactory));

        builder.RegisterInstance<IHttpClientFactory>(new FakeHttpClientFactory(client));

        return builder.Build();
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body)
        };
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string body)
    {
        var response = CreateResponse(statusCode, body);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return response;
    }

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return client;
        }
    }

    private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return responseFactory(request, cancellationToken);
        }
    }

    private sealed class TestResponse
    {
        public string? Message { get; set; }
    }
}
