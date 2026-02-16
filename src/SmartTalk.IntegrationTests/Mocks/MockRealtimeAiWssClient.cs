using System.Net.WebSockets;
using System.Text;
using SmartTalk.Core.Services.RealtimeAiV2.Wss;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.IntegrationTests.Mocks;

public class MockRealtimeAiWssClient : IRealtimeAiWssClient
{
    private readonly Queue<string> _responseQueue = new();

    public RealtimeAiProvider Provider => RealtimeAiProvider.OpenAi;
    public WebSocketState CurrentState { get; private set; } = WebSocketState.None;
    public Uri EndpointUri { get; private set; }

    public event Func<string, Task> MessageReceivedAsync;
    public event Func<WebSocketState, string, Task> StateChangedAsync;
    public event Func<Exception, Task> ErrorOccurredAsync;

    public List<byte[]> SentMessages { get; } = new();

    public void EnqueueMessage(string json) => _responseQueue.Enqueue(json);

    public Task ConnectAsync(Uri endpointUri, Dictionary<string, string> customHeaders, CancellationToken cancellationToken)
    {
        EndpointUri = endpointUri;
        CurrentState = WebSocketState.Open;
        return Task.CompletedTask;
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        SentMessages.Add(Encoding.UTF8.GetBytes(message));

        if (_responseQueue.Count > 0 && MessageReceivedAsync != null)
            await MessageReceivedAsync.Invoke(_responseQueue.Dequeue()).ConfigureAwait(false);
    }

    public Task DisconnectAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        CurrentState = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
