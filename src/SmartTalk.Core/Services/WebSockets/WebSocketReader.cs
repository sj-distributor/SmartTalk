using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace SmartTalk.Core.Services.WebSockets;

public static class WebSocketReader
{
    public static async Task RunAsync(WebSocket webSocket, Func<string, Task> onMessage, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                ValueWebSocketReceiveResult result;

                do
                {
                    result = await webSocket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close) return;

                    ms.Write(buffer, 0, result.Count);

                } while (!result.EndOfMessage);

                if (ms.Length > 0)
                    await onMessage(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length)).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
