using System.Net.WebSockets;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Wss.OpenAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The provider transport had no tests at all — 228 lines carrying every call's audio, exercised only
/// indirectly through a fake that replaces it.
///
/// <para>Nothing here needs a network: the cases that matter are state transitions and lifecycle,
/// and an unroutable loopback port fails fast enough to drive the failure paths directly.</para>
/// </summary>
public class OpenAiRealtimeAiWssClientTests
{
    // Port 1 on loopback: nothing listens, and the connect refuses immediately rather than hanging.
    private static readonly Uri Unroutable = new("ws://127.0.0.1:1/realtime");

    [Fact]
    public void FreshClient_ShouldReportNoConnection()
    {
        var sut = new OpenAiRealtimeAiWssClient();

        sut.CurrentState.ShouldBe(WebSocketState.None);
        sut.EndpointUri.ShouldBeNull();
    }

    [Fact]
    public async Task SendingBeforeConnected_ShouldRaiseTheErrorEventAndThrow()
    {
        // The engine's send path treats a throw here as a transport failure. Swallowing it instead
        // would let audio vanish silently.
        var sut = new OpenAiRealtimeAiWssClient();

        Exception raised = null;
        sut.ErrorOccurredAsync += ex => { raised = ex; return Task.CompletedTask; };

        await Should.ThrowAsync<InvalidOperationException>(() => sut.SendMessageAsync("{}", CancellationToken.None));

        raised.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task DisconnectingWithoutEverConnecting_ShouldNotThrow()
    {
        // Cleanup runs on paths where connect never succeeded; it must be safe there.
        var sut = new OpenAiRealtimeAiWssClient();

        await Should.NotThrowAsync(() => sut.DisconnectAsync(WebSocketCloseStatus.NormalClosure, "never up", CancellationToken.None));
    }

    [Fact]
    public async Task FailedConnect_ShouldSurfaceTheErrorAndRethrow()
    {
        var sut = new OpenAiRealtimeAiWssClient();

        var errorRaised = false;
        sut.ErrorOccurredAsync += _ => { errorRaised = true; return Task.CompletedTask; };

        await Should.ThrowAsync<WebSocketException>(
            () => sut.ConnectAsync(Unroutable, null, CancellationToken.None));

        errorRaised.ShouldBeTrue("the engine learns about a failed provider connect only through this event");
    }

    /// <summary>
    /// Pins a latent defect rather than a requirement, so the work that trips over it finds it
    /// already described.
    ///
    /// <para>The constructor creates the ClientWebSocket, and cleanup after a failed connect sets the
    /// field to null with a comment saying a new one will be created on the next ConnectAsync —
    /// but nothing creates one, so the next attempt dereferences null. It is invisible today because
    /// the client is scoped per call and only ever connects once. It becomes a crash the moment
    /// reconnect exists, which is precisely the change that would rely on it.</para>
    /// </summary>
    [Fact]
    public async Task ReconnectingAfterAFailedConnect_CurrentlyThrowsNullReference()
    {
        var sut = new OpenAiRealtimeAiWssClient();

        await Should.ThrowAsync<WebSocketException>(
            () => sut.ConnectAsync(Unroutable, null, CancellationToken.None));

        await Should.ThrowAsync<NullReferenceException>(
            () => sut.ConnectAsync(Unroutable, null, CancellationToken.None));
    }

    [Fact]
    public async Task DisposingTwice_ShouldNotThrow()
    {
        var sut = new OpenAiRealtimeAiWssClient();

        await sut.DisposeAsync();

        await Should.NotThrowAsync(async () => await sut.DisposeAsync());
    }
}
