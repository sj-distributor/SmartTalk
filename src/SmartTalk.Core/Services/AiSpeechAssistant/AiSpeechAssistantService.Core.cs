using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantService
{
    public async Task ConnectSpeechRealtimeAsync(CancellationToken cancellationToken)
    {
        await HandleWebSocket();
    }
    
    private static readonly HashSet<string> LOG_EVENT_TYPES = new() 
    { 
        "session.updated", 
        "response.audio.delta" 
    };

    public async Task HandleWebSocket(HttpContext context, CancellationToken cancellationToken)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        Console.WriteLine("Client connected");

        using var openAiWs = new ClientWebSocket();
        var openAiUri = new Uri("wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-10-01");
        
        openAiWs.Options.SetRequestHeader("Authorization", $"Bearer {_openAiSettings.ApiKey}");
        openAiWs.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

        await openAiWs.ConnectAsync(openAiUri, CancellationToken.None);
        await SendSessionUpdate(openAiWs);

        var streamSid = string.Empty;
        var cts = new CancellationTokenSource();

        // Start bidirectional communication
        var receiveFromTwilio = ReceiveFromTwilioAsync(webSocket, openAiWs, cts.Token, () => streamSid, sid => streamSid = sid);
        var sendToTwilio = SendToTwilioAsync(webSocket, openAiWs, cts.Token, () => streamSid);

        await Task.WhenAll(receiveFromTwilio, sendToTwilio);
    }

    private async Task SendSessionUpdate(ClientWebSocket openAiWs)
    {
        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                audio = new { codec = "pcm", sample_rate = 16000 },
                transcript = new { format = "text" }
            }
        };

        var json = JsonSerializer.Serialize(sessionUpdate);
        var buffer = Encoding.UTF8.GetBytes(json);
        await openAiWs.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task ReceiveFromTwilioAsync(
        WebSocket twilioWs, 
        ClientWebSocket openAiWs,
        CancellationToken ct,
        Func<string> getStreamSid,
        Action<string> setStreamSid)
    {
        var buffer = new byte[1024 * 4];
        try
        {
            while (twilioWs.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await twilioWs.ReceiveAsync(buffer, ct);
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                if (root.TryGetProperty("event", out var eventType))
                {
                    if (eventType.ValueEquals("media") && openAiWs.State == WebSocketState.Open)
                    {
                        var mediaPayload = root.GetProperty("media").GetProperty("payload").GetString();
                        var audioMessage = new 
                        {
                            type = "input_audio_buffer.append",
                            audio = mediaPayload
                        };
                        
                        var json = JsonSerializer.Serialize(audioMessage);
                        var aiBuffer = Encoding.UTF8.GetBytes(json);
                        await openAiWs.SendAsync(aiBuffer, WebSocketMessageType.Text, true, ct);
                    }
                    else if (eventType.ValueEquals("start"))
                    {
                        var streamId = root.GetProperty("start").GetProperty("streamSid").GetString();
                        setStreamSid(streamId);
                        Console.WriteLine($"Incoming stream started: {streamId}");
                    }
                }
            }
        }
        catch (WebSocketException)
        {
            Console.WriteLine("Client disconnected");
            await openAiWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", ct);
        }
    }

    private async Task SendToTwilioAsync(
        WebSocket twilioWs,
        ClientWebSocket openAiWs,
        CancellationToken ct,
        Func<string> getStreamSid)
    {
        var buffer = new byte[1024 * 4];
        try
        {
            while (openAiWs.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await openAiWs.ReceiveAsync(buffer, ct);
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var type))
                {
                    if (LOG_EVENT_TYPES.Contains(type.GetString()))
                    {
                        Console.WriteLine($"Received event: {type.GetString()} - {message}");
                    }

                    if (type.ValueEquals("response.audio.delta") && 
                       root.TryGetProperty("delta", out var delta))
                    {
                        try
                        {
                            var audioPayload = Convert.ToBase64String(
                                Convert.FromBase64String(delta.GetString()));
                            
                            var audioMessage = new 
                            {
                                @event = "media",
                                streamSid = getStreamSid(),
                                media = new { payload = audioPayload }
                            };

                            var json = JsonSerializer.Serialize(audioMessage);
                            var twilioBuffer = Encoding.UTF8.GetBytes(json);
                            await twilioWs.SendAsync(twilioBuffer, WebSocketMessageType.Text, true, ct);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing audio: {ex}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SendToTwilio: {ex}");
        }
    }
}