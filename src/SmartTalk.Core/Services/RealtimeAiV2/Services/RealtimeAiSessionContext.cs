using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Recording;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Core.Services.RealtimeAiV2.Wss;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

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
    public IRealtimeAiTtsProvider TtsProvider { get; set; }
    public CancellationTokenSource SessionCts { get; set; }

    // Negotiated once at connect (OutputModeNegotiator) and reused for the session — never re-sniffed.
    public RealtimeAiOutputMode OutputMode { get; set; }

    // Runtime state
    public int Round { get; set; }
    public volatile bool IsAiSpeaking;
    public volatile bool IsClientAudioToProviderSuspended;
    public bool IsProviderResponseInProgress;
    public bool HasPendingProviderResponseTrigger;
    public bool CurrentResponseHasTextOutput;
    public bool CurrentResponseTextDoneHandled;
    public bool CurrentResponseProviderTurnCompleted;
    public bool CurrentResponseTtsSynthesisCompleted;
    public bool CurrentResponseTurnCompletedHandled;

    // Accumulates the assistant's text output for the current turn so external-TTS mode can
    // surface the AI side of the transcript (no output_audio_transcript events arrive there).
    public StringBuilder CurrentResponseTextBuilder { get; } = new();

    // Barge-in state: item_id of the in-flight assistant turn + stream-time anchor.
    // Both cleared after the truncate is sent or the turn completes.
    public string LastAssistantItemId { get; set; }
    public long? LatestMediaTimestamp { get; set; }
    public long? ResponseStartTimestampTwilio { get; set; }

    // Recording — buffer encapsulates the previous (MemoryStream + SemaphoreSlim)
    // pair behind a single interface; PR 3.2 will swap implementations via env var.
    public IRecordingBuffer AudioBuffer { get; set; }

    // Transcriptions
    public ConcurrentQueue<(AiSpeechAssistantSpeaker Speaker, string Text)> Transcriptions { get; } = new();

    // Synchronization
    public SemaphoreSlim WsSendLock { get; } = new(1, 1);
    public SemaphoreSlim ProviderResponseStateLock { get; } = new(1, 1);
    public SemaphoreSlim TurnCompletionStateLock { get; } = new(1, 1);

    // Actions exposed to consumer callbacks
    public RealtimeAiSessionActions SessionActions { get; set; }
}
