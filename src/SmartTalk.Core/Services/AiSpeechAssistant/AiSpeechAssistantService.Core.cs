using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantService
{
    public async Task ConnectSpeechRealtimeAsync(ConnectSpeechRealtimeCommand command, CancellationToken cancellationToken)
    {
        await HandleWebSocket(command.HttpContext, cancellationToken);
    }
    
    private static readonly HashSet<string> LOG_EVENT_TYPES = new() 
    { 
        "session.updated", 
        "response.audio.delta" 
    };

    public async Task HandleWebSocket(HttpContext context, CancellationToken cancellationToken)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        Log.Information("Client connected");

        using var openAiWs = new ClientWebSocket();
        var openAiUri = new Uri("wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-12-17");
        
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
                turn_detection = new { type = "server_vad", interrupt_response = true, create_response = true },
                input_audio_format = "g711_ulaw",
                output_audio_format = "g711_ulaw",
                voice = "alloy",
                instructions = "You are a Moon house restaurant assistant",
                modalities = new[] { "text", "audio" },
                temperature = 0.8
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
        var buffer = new byte[1024 * 10];
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
                        Log.Information($"Incoming stream started: {streamId}");
                    }
                }
            }
        }
        catch (WebSocketException)
        {
            Log.Information("Client disconnected");
            await openAiWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", ct);
        }
    }

    private async Task SendToTwilioAsync(
        WebSocket twilioWs,
        ClientWebSocket openAiWs,
        CancellationToken ct,
        Func<string> getStreamSid)
    {
        var buffer = new byte[1024 * 30];
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
                    Log.Information("Received event: {type} - {@message}", type.GetString(), JsonConvert.DeserializeObject<object>(message));

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
                            Log.Error($"Error processing audio: {ex}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error in SendToTwilio: {ex}");
        }
    }
}