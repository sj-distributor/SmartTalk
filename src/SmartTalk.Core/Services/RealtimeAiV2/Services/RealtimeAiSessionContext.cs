using System.Collections.Concurrent;
using System.Net.WebSockets;
using SmartTalk.Core.Services.RealtimeAiV2.Wss;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public class RealtimeAiSessionContext
{
    // Identity
    public string SessionId { get; } = Guid.NewGuid().ToString();
    public string StreamSid { get; } = Guid.NewGuid().ToString("N");

    // Configuration
    public RealtimeSessionOptions Options { get; set; }

    // Connection
    public WebSocket WebSocket { get; set; }
    public IRealtimeAiWssClient WssClient { get; set; }
    public IRealtimeAiProviderAdapter Adapter { get; set; }
    public CancellationTokenSource SessionCts { get; set; }

    // Runtime state
    public int Round { get; set; }
    public volatile bool IsAiSpeaking;

    // Recording
    public MemoryStream AudioBuffer { get; set; }
    public SemaphoreSlim BufferLock { get; } = new(1, 1);

    // Transcriptions
    public ConcurrentQueue<(AiSpeechAssistantSpeaker Speaker, string Text)> Transcriptions { get; } = new();

    // Synchronization
    public SemaphoreSlim WsSendLock { get; } = new(1, 1);
}