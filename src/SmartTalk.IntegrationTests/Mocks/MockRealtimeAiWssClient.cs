using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using SmartTalk.Core.Services.RealtimeAiV2.Wss;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.IntegrationTests.Mocks;

/// <summary>
/// Provider-socket double for the integration tier.
///
/// <para>Until the Simulate* methods below existed, this mock could only replay canned responses in
/// reaction to an outbound send — <c>StateChangedAsync</c> and <c>ErrorOccurredAsync</c> were
/// declared but never raised, which the compiler reported as two CS0067 warnings. That made the
/// failure modes that actually end live calls (the provider socket dropping, a transport error
/// mid-turn) structurally untestable at this tier, even though the integration suite is the only
/// place the Twilio → mediator → consumer → engine path runs end to end.</para>
///
/// <para>Queues are concurrent because the engine drains them from the provider receive loop while a
/// test may still be enqueuing from its own thread.</para>
/// </summary>
public class MockRealtimeAiWssClient : IRealtimeAiWssClient
{
    private readonly ConcurrentQueue<string> _responseQueue = new();
    private readonly ConcurrentQueue<string> _sendTriggeredQueue = new();

    public RealtimeAiProvider Provider => RealtimeAiProvider.OpenAi;
    public WebSocketState CurrentState { get; private set; } = WebSocketState.None;
    public Uri EndpointUri { get; private set; }

    public event Func<string, Task> MessageReceivedAsync;
    public event Func<WebSocketState, string, Task> StateChangedAsync;
    public event Func<Exception, Task> ErrorOccurredAsync;

    public List<byte[]> SentMessages { get; } = new();

    public void EnqueueMessage(string json) => _responseQueue.Enqueue(json);
    public void EnqueueSendTriggeredMessage(string json) => _sendTriggeredQueue.Enqueue(json);

    public Task ConnectAsync(Uri endpointUri, Dictionary<string, string> customHeaders, CancellationToken cancellationToken)
    {
        EndpointUri = endpointUri;
        CurrentState = WebSocketState.Open;
        return Task.CompletedTask;
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        SentMessages.Add(Encoding.UTF8.GetBytes(message));

        if (_sendTriggeredQueue.TryDequeue(out var triggered) && MessageReceivedAsync != null)
        {
            await MessageReceivedAsync.Invoke(triggered).ConfigureAwait(false);
            return;
        }

        while (_responseQueue.TryDequeue(out var queued) && MessageReceivedAsync != null)
            await MessageReceivedAsync.Invoke(queued).ConfigureAwait(false);
    }

    public Task DisconnectAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        DisconnectCallCount++;
        CurrentState = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public int DisconnectCallCount { get; private set; }

    /// <summary>
    /// Push a provider event the test did not have to trigger with a send. Needed for anything the
    /// provider originates on its own — speech-detected, transcription, an unsolicited error frame.
    /// </summary>
    public async Task SimulateMessageReceivedAsync(string rawMessage)
    {
        if (MessageReceivedAsync != null)
            await MessageReceivedAsync.Invoke(rawMessage).ConfigureAwait(false);
    }

    /// <summary>
    /// Drop the provider connection the way a 1011 or an LB reset does. Drives the engine's
    /// critical-ConnectionLost path, which today ends the caller's call.
    /// </summary>
    public async Task SimulateStateChangedAsync(WebSocketState newState, string reason)
    {
        CurrentState = newState;

        if (StateChangedAsync != null)
            await StateChangedAsync.Invoke(newState, reason).ConfigureAwait(false);
    }

    /// <summary>Raise a transport-level error, as the real clients do from their receive loop.</summary>
    public async Task SimulateErrorOccurredAsync(Exception ex)
    {
        if (ErrorOccurredAsync != null)
            await ErrorOccurredAsync.Invoke(ex).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
