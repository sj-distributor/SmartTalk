using System.Collections.Concurrent;
using System.Net.WebSockets;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Recording;
using SmartTalk.Core.Services.RealtimeAiV2.Wss;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public class RealtimeAiSessionContext
{
    // Identity
    public string SessionId { get; } = Guid.NewGuid().ToString();

    // Configuration
    public RealtimeSessionOptions Options { get; set; }

    // Connection
    public WebSocket WebSocket { get; set; }
    public IRealtimeAiWssClient WssClient { get; set; }
    public IRealtimeAiClientAdapter ClientAdapter { get; set; }
    
    public IRealtimeAiProviderAdapter ProviderAdapter { get; set; }
    public CancellationTokenSource SessionCts { get; set; }

    // Runtime state
    public int Round { get; set; }
    public volatile bool IsAiSpeaking;
    public volatile bool IsClientAudioToProviderSuspended;
    public bool IsProviderResponseInProgress;
    public bool HasPendingProviderResponseTrigger;

    /// <summary>
    /// The provider <c>item_id</c> of the AI's current in-flight assistant turn,
    /// captured from every <see cref="Messages.Enums.RealtimeAi.RealtimeAiWssEventType.ResponseAudioDelta"/>
    /// that carries one. Phase 10.3 will read this to build the OpenAI
    /// <c>conversation.interrupt</c> message at user-barge-in time. <c>null</c>
    /// between turns (cleared by <c>OnAiTurnCompletedAsync</c>) so a stale id
    /// from a previous turn cannot be sent on the next interrupt opportunity.
    /// Phase 10.1 wires the tracking; today no consumer reads it.
    /// </summary>
    public string LastAssistantItemId { get; set; }

    // Recording — buffer encapsulates the previous (MemoryStream + SemaphoreSlim)
    // pair behind a single interface; PR 3.2 will swap implementations via env var.
    public IRecordingBuffer AudioBuffer { get; set; }

    // Transcriptions
    public ConcurrentQueue<(AiSpeechAssistantSpeaker Speaker, string Text)> Transcriptions { get; } = new();

    // Synchronization
    public SemaphoreSlim WsSendLock { get; } = new(1, 1);
    public SemaphoreSlim ProviderResponseStateLock { get; } = new(1, 1);

    // Actions exposed to consumer callbacks
    public RealtimeAiSessionActions SessionActions { get; set; }
}
