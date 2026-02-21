using System.Collections.Concurrent;
using System.Net.WebSockets;
using SmartTalk.Core.Services.RealtimeAiV2.Wss;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Fakes;

/// <summary>
/// Test double for the provider WebSocket client. Captures sent messages and
/// allows tests to simulate provider events (messages, state changes, errors).
/// </summary>
public class FakeRealtimeAiWssClient : IRealtimeAiWssClient
{
    private WebSocketState _currentState = WebSocketState.None;

    public RealtimeAiProvider Provider => RealtimeAiProvider.OpenAi;
    public WebSocketState CurrentState => _currentState;
    public Uri? EndpointUri { get; private set; }

    public event Func<string, Task>? MessageReceivedAsync;
    public event Func<WebSocketState, string, Task>? StateChangedAsync;
    public event Func<Exception, Task>? ErrorOccurredAsync;

    /// <summary>All messages the service sent to the provider.</summary>
    public ConcurrentQueue<string> SentMessages { get; } = new();

    /// <summary>When true, ConnectAsync will throw.</summary>
    public bool ShouldFailConnect { get; set; }

    /// <summary>State to transition to after a successful ConnectAsync. Default: Open.</summary>
    public WebSocketState StateAfterConnect { get; set; } = WebSocketState.Open;

    public int ConnectCallCount { get; private set; }
    public int DisconnectCallCount { get; private set; }

    public Task ConnectAsync(Uri endpointUri, Dictionary<string, string> customHeaders, CancellationToken cancellationToken)
    {
        ConnectCallCount++;
        EndpointUri = endpointUri;

        if (ShouldFailConnect)
            throw new WebSocketException("Simulated connection failure");

        _currentState = StateAfterConnect;
        return Task.CompletedTask;
    }

    public Task SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        SentMessages.Enqueue(message);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        DisconnectCallCount++;
        _currentState = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    /// <summary>Fire the MessageReceivedAsync event as if the provider sent a message.</summary>
    public async Task SimulateMessageReceivedAsync(string rawMessage)
    {
        if (MessageReceivedAsync != null)
            await MessageReceivedAsync.Invoke(rawMessage).ConfigureAwait(false);
    }

    /// <summary>Fire the StateChangedAsync event as if the provider connection state changed.</summary>
    public async Task SimulateStateChangedAsync(WebSocketState newState, string reason)
    {
        _currentState = newState;
        if (StateChangedAsync != null)
            await StateChangedAsync.Invoke(newState, reason).ConfigureAwait(false);
    }

    /// <summary>Fire the ErrorOccurredAsync event as if a provider WebSocket error occurred.</summary>
    public async Task SimulateErrorOccurredAsync(Exception ex)
    {
        if (ErrorOccurredAsync != null)
            await ErrorOccurredAsync.Invoke(ex).ConfigureAwait(false);
    }

    /// <summary>Set the internal state without firing events (for test setup).</summary>
    public void SetState(WebSocketState state) => _currentState = state;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
