using System.Net.WebSockets;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

/// <summary>
/// Best-effort WebSocket cleanup helper. Used by ConnectAsync's finally block
/// to ensure the Twilio WebSocket is closed even when an unknown exception
/// escapes the orchestrator and would otherwise leave the socket dangling
/// for the platform's idle timeout (~30s of silence for the caller).
///
/// <para>Contract:</para>
/// <list type="bullet">
///   <item>null WebSocket → no-op</item>
///   <item>Already-closed states (Closed, Aborted, None) → no-op</item>
///   <item>Live or half-closed states (Open, CloseReceived, CloseSent, Connecting) → CloseAsync</item>
///   <item>Any exception during CloseAsync → swallowed (we cannot do better than best-effort here)</item>
///   <item>2-second timeout on CloseAsync to prevent deadlock if the peer never acknowledges</item>
/// </list>
/// </summary>
public class TryCloseTwilioWebSocketAsyncTests
{
    [Fact]
    public async Task TryCloseTwilioWebSocketAsync_NullWebSocket_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(
            () => AiSpeechAssistantConnectService.TryCloseTwilioWebSocketAsync(null));

        ex.ShouldBeNull();
    }

    [Theory]
    [InlineData(WebSocketState.Closed)]
    [InlineData(WebSocketState.Aborted)]
    [InlineData(WebSocketState.None)]
    public async Task TryCloseTwilioWebSocketAsync_AlreadyClosedState_DoesNotCallCloseAsync(WebSocketState state)
    {
        var ws = Substitute.For<WebSocket>();
        ws.State.Returns(state);

        await AiSpeechAssistantConnectService.TryCloseTwilioWebSocketAsync(ws);

        await ws.DidNotReceive().CloseAsync(
            Arg.Any<WebSocketCloseStatus>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(WebSocketState.Open)]
    [InlineData(WebSocketState.CloseReceived)]
    [InlineData(WebSocketState.CloseSent)]
    [InlineData(WebSocketState.Connecting)]
    public async Task TryCloseTwilioWebSocketAsync_LiveOrHalfClosedState_CallsCloseAsyncWithNormalClosure(WebSocketState state)
    {
        var ws = Substitute.For<WebSocket>();
        ws.State.Returns(state);

        await AiSpeechAssistantConnectService.TryCloseTwilioWebSocketAsync(ws);

        await ws.Received(1).CloseAsync(
            WebSocketCloseStatus.NormalClosure, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryCloseTwilioWebSocketAsync_WebSocketExceptionDuringClose_Swallowed()
    {
        var ws = Substitute.For<WebSocket>();
        ws.State.Returns(WebSocketState.Open);
        ws.CloseAsync(Arg.Any<WebSocketCloseStatus>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new WebSocketException("peer already closed"));

        var ex = await Record.ExceptionAsync(
            () => AiSpeechAssistantConnectService.TryCloseTwilioWebSocketAsync(ws));

        ex.ShouldBeNull();
    }

    [Fact]
    public async Task TryCloseTwilioWebSocketAsync_ObjectDisposedExceptionDuringClose_Swallowed()
    {
        var ws = Substitute.For<WebSocket>();
        ws.State.Returns(WebSocketState.Open);
        ws.CloseAsync(Arg.Any<WebSocketCloseStatus>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new ObjectDisposedException(nameof(WebSocket)));

        var ex = await Record.ExceptionAsync(
            () => AiSpeechAssistantConnectService.TryCloseTwilioWebSocketAsync(ws));

        ex.ShouldBeNull();
    }

    [Fact]
    public async Task TryCloseTwilioWebSocketAsync_OperationCanceledDuringClose_Swallowed()
    {
        var ws = Substitute.For<WebSocket>();
        ws.State.Returns(WebSocketState.Open);
        ws.CloseAsync(Arg.Any<WebSocketCloseStatus>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException("timeout"));

        var ex = await Record.ExceptionAsync(
            () => AiSpeechAssistantConnectService.TryCloseTwilioWebSocketAsync(ws));

        ex.ShouldBeNull();
    }
}
