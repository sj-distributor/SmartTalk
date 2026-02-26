using System.Net.WebSockets;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Wss;

public interface IRealtimeAiWssClient : IScopedDependency, IAsyncDisposable 
{
    RealtimeAiProvider Provider { get; }
    WebSocketState CurrentState { get; }
    Uri EndpointUri { get; }

    event Func<string, Task> MessageReceivedAsync;
    event Func<WebSocketState, string, Task> StateChangedAsync;
    event Func<Exception, Task> ErrorOccurredAsync;

    Task ConnectAsync(Uri endpointUri, Dictionary<string, string> customHeaders, CancellationToken cancellationToken);
    Task SendMessageAsync(string message, CancellationToken cancellationToken);
    Task DisconnectAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken);
}