using Serilog;
using System.Text;
using Newtonsoft.Json;
using System.Text.Json;
using SmartTalk.Core.Ioc;
using System.Net.WebSockets;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Core.Services.RealtimeAi.Wss;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAi.Adapters;
using SmartTalk.Messages.Enums.RealtimeAi;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.RealtimeAi.Services;

public interface IRealtimeAiService : IScopedDependency
{
    Task RealtimeAiConnectAsync(WebSocket webSocket, Domain.AISpeechAssistant.AiSpeechAssistant assistant, string initialPrompt, CancellationToken cancellationToken);
}

public class RealtimeAiService : IRealtimeAiService
{
    private readonly IRealtimeAiSwitcher _realtimeAiSwitcher;
    private readonly IRealtimeAiAudioCodecAdapter _audioCodecAdapter;

    private string _streamSid;
    private WebSocket _webSocket;
    private CancellationTokenSource _realtimeAiCts;
    private IRealtimeAiConversationEngine _conversationEngine;
    private Domain.AISpeechAssistant.AiSpeechAssistant _currentAssistant;

    public RealtimeAiService(IRealtimeAiSwitcher realtimeAiSwitcher, IRealtimeAiAudioCodecAdapter audioCodecAdapter, IRealtimeAiConversationEngine conversationEngine)
    {
        _realtimeAiSwitcher = realtimeAiSwitcher;
        _audioCodecAdapter = audioCodecAdapter;
        _conversationEngine = conversationEngine;

        _webSocket = null;
        _currentAssistant = new Domain.AISpeechAssistant.AiSpeechAssistant();
    }

    public async Task RealtimeAiConnectAsync(WebSocket webSocket, Domain.AISpeechAssistant.AiSpeechAssistant assistant, string initialPrompt, CancellationToken cancellationToken)
    {
        _webSocket = webSocket;
        _currentAssistant = assistant;
        _streamSid = Guid.NewGuid().ToString("N");
        _realtimeAiCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        _conversationEngine.SessionStatusChangedAsync += OnAiSessionStatusChangedAsync;
        
        BuildConversationEngine(assistant.ModelProvider);
        
        await _conversationEngine.StartSessionAsync(assistant, initialPrompt, cancellationToken);
        
        await ReceiveFromWebSocketClientAsync(cancellationToken);
    }

    private void BuildConversationEngine(AiSpeechAssistantProvider provider)
    {
        var client = _realtimeAiSwitcher.WssClient(provider);
        var adapter = _realtimeAiSwitcher.ProviderAdapter(provider);
        
        _conversationEngine = new RealtimeAiConversationEngine(adapter, client);
        _conversationEngine.AiAudioOutputReadyAsync += OnAiAudioOutputReadyAsync;
    }
    
    private async Task ReceiveFromWebSocketClientAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        try
        {
            while (_webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                using var ms = new MemoryStream();
                
                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _conversationEngine.EndSessionAsync("Disconnect From RealtimeAi");
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client acknowledges close", CancellationToken.None);
                        return;
                    }
                    
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
        
                ms.Seek(0, SeekOrigin.Begin);
                var rawMessage = Encoding.UTF8.GetString(ms.ToArray());
        
                Log.Debug("ReceiveFromRealtimeClientAsync raw message: {@Message}", rawMessage);
        
                try
                {
                    using var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(rawMessage);
                    var payload = jsonDocument?.RootElement.GetProperty("media").GetProperty("payload").GetString();
        
                    if (!string.IsNullOrWhiteSpace(payload))
                    {
                        await _conversationEngine.SendAudioChunkAsync(new RealtimeAiWssAudioData
                        {
                            Base64Payload = payload
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        Log.Warning("ReceiveFromRealtimeClientAsync: payload is null or empty.");
                    }
                }
                catch (JsonException jsonEx)
                {
                    Log.Error("Failed to parse incoming JSON: {Error}. Raw: {Raw}", jsonEx.Message, rawMessage);
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Error("Receive from realtime error: {@ex}", ex);
        }
    }
    
    private async Task OnAiAudioOutputReadyAsync(RealtimeAiWssAudioData aiAudioData)
    {
        if (aiAudioData == null || string.IsNullOrEmpty(aiAudioData.Base64Payload)) return;

        Log.Information("Realtime output: {@Output} 准备发送。", aiAudioData);
        
        var audioDelta = new
        {
            @event = "media",
            streamSid = _streamSid,
            media = new { payload = aiAudioData.Base64Payload }
        };

        await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(audioDelta))), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    private Task OnAiSessionStatusChangedAsync(RealtimeAiWssEventType type, object data)
    {
        // ... (同前) ...
        if (type == RealtimeAiWssEventType.SessionInitialized)
        {
            Log.Information("TwilioHandler: AI 会话已成功初始化，可以开始双向通信。"); // TwilioHandler: AI session successfully initialized, bidirectional communication can begin.
        }
        else if (type == RealtimeAiWssEventType.SessionUpdateFailed)
        {
            Log.Error("TwilioHandler: AI 会话初始化或更新失败: {@EventData}", data); // TwilioHandler: AI session initialization or update failed: {@EventData}
        }
        return Task.CompletedTask;
    }
}