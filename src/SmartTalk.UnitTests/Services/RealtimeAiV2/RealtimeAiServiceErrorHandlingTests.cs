using System.Net.WebSockets;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class RealtimeAiServiceErrorHandlingTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task ProviderError_Critical_ClientNotifiedAndSessionDisconnected()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.Error,
                Data = new RealtimeAiErrorData
                {
                    Code = "server_error",
                    Message = "Internal server error",
                    IsCritical = true
                }
            });

        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"error\"}");
        await Task.Delay(200);

        // Critical error should disconnect, which cancels the session CTS,
        // causing the read loop to exit
        FakeWs.EnqueueClose();
        await sessionTask;

        // Error message should have been sent to client
        ClientAdapter.Received().BuildErrorMessage("server_error", "Internal server error", Arg.Any<string>());

        // Provider should have been disconnected
        FakeWssClient.DisconnectCallCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ProviderError_NonCritical_ClientNotifiedSessionContinues()
    {
        var parseCallCount = 0;
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(_ =>
            {
                parseCallCount++;
                return parseCallCount == 1
                    ? new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.Error,
                        Data = new RealtimeAiErrorData
                        {
                            Code = "rate_limit",
                            Message = "Rate limited",
                            IsCritical = false
                        }
                    }
                    : new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                        Data = new List<RealtimeAiWssFunctionCallData>()
                    };
            });

        var sessionTask = await StartSessionInBackgroundAsync();

        // Non-critical error
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"error\"}");
        await Task.Delay(100);

        // Session should still be alive - send another event to prove it
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        ClientAdapter.Received().BuildErrorMessage("rate_limit", "Rate limited", Arg.Any<string>());
        // TurnCompleted was also processed, proving session continued
        ClientAdapter.Received().BuildTurnCompletedMessage(Arg.Any<string>());
    }

    [Fact]
    public async Task ProviderStateChange_Closed_TreatedAsCriticalError()
    {
        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateStateChangedAsync(WebSocketState.Closed, "Server closed connection");
        await Task.Delay(200);

        FakeWs.EnqueueClose();
        await sessionTask;

        ClientAdapter.Received().BuildErrorMessage("ConnectionLost", Arg.Is<string>(s => s.Contains("Server closed connection")), Arg.Any<string>());
    }

    [Fact]
    public async Task ProviderStateChange_Aborted_TreatedAsCriticalError()
    {
        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateStateChangedAsync(WebSocketState.Aborted, "Connection aborted");
        await Task.Delay(200);

        FakeWs.EnqueueClose();
        await sessionTask;

        ClientAdapter.Received().BuildErrorMessage("ConnectionLost", Arg.Is<string>(s => s.Contains("Connection aborted")), Arg.Any<string>());
    }

    [Fact]
    public async Task ProviderWssError_TreatedAsCriticalError()
    {
        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateErrorOccurredAsync(new InvalidOperationException("Socket died"));
        await Task.Delay(200);

        FakeWs.EnqueueClose();
        await sessionTask;

        ClientAdapter.Received().BuildErrorMessage("ProviderClientError", "Socket died", Arg.Any<string>());
    }

    [Fact]
    public async Task ClientWebSocketException_CleanupStillRuns()
    {
        string? endedSessionId = null;
        var options = CreateDefaultOptions(o =>
        {
            o.OnSessionEndedAsync = id => { endedSessionId = id; return Task.CompletedTask; };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        // Simulate a WebSocketException during the read loop
        FakeWs.EnqueueError(new WebSocketException("Connection reset"));
        await Task.Delay(200);

        // The read loop catches WebSocketException and falls through to cleanup
        // EnqueueClose in case the loop needs it to terminate
        FakeWs.EnqueueClose();
        await sessionTask;

        // Cleanup should have run: OnSessionEndedAsync should have been called
        endedSessionId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ClientMessage_JsonException_SessionContinues()
    {
        var callCount = 0;
        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    throw new System.Text.Json.JsonException("Malformed JSON");
                return (RealtimeAiClientMessageType.Text, "valid text");
            });

        var sessionTask = await StartSessionInBackgroundAsync();

        // First message: ParseMessage throws JsonException → caught and logged
        FakeWs.EnqueueClientMessage("not-json{{{");
        await Task.Delay(50);

        // Second message: should be processed normally, proving session survived
        FakeWs.EnqueueClientMessage("{\"text\":\"valid text\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Second message was processed
        ProviderAdapter.Received().BuildTextUserMessage("valid text", Arg.Any<string>());
    }

    [Fact]
    public async Task ProviderEventHandler_ThrowsException_CaughtAndSessionContinues()
    {
        var parseCallCount = 0;
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(_ =>
            {
                parseCallCount++;
                return parseCallCount == 1
                    ? new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SessionInitialized }
                    : new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                        Data = new List<RealtimeAiWssFunctionCallData>()
                    };
            });

        var options = CreateDefaultOptions(o =>
        {
            // OnSessionReadyAsync throws — will be caught by the outer try-catch in OnWssMessageReceivedAsync
            o.OnSessionReadyAsync = _ => throw new InvalidOperationException("Callback exploded");
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        // First event: SessionInitialized → OnSessionReadyAsync throws → caught
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"session.created\"}");
        await Task.Delay(50);

        // Second event: TurnCompleted → should process normally, proving session survived
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        ClientAdapter.Received().BuildTurnCompletedMessage(Arg.Any<string>());
    }

    [Fact]
    public async Task ProviderStateChange_NonCriticalState_NoErrorSent()
    {
        var sessionTask = await StartSessionInBackgroundAsync();

        // Connecting is neither Closed nor Aborted → should be a no-op
        await FakeWssClient.SimulateStateChangedAsync(WebSocketState.Connecting, "Reconnecting");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // No error message should have been sent to client
        ClientAdapter.DidNotReceive().BuildErrorMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
