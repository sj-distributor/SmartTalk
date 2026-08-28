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
    // Identity — the consumer may supply this via RealtimeSessionOptions.SessionId so its own
    // pre-connect log lines share the value; otherwise the engine mints one.
    public string SessionId { get; init; } = Guid.NewGuid().ToString();

    // Configuration
    public RealtimeSessionOptions Options { get; set; }

    // Connection
    public WebSocket WebSocket { get; set; }
    public IRealtimeAiWssClient WssClient { get; set; }
    public IRealtimeAiClientAdapter ClientAdapter { get; set; }
    
    public IRealtimeAiProviderAdapter ProviderAdapter { get; set; }
    public IRealtimeAiTtsProvider TtsProvider { get; set; }
    public CancellationTokenSource SessionCts { get; set; }

    /// <summary>
    /// Claim flag for provider teardown, taken with Interlocked. A provider drop on a live session
    /// reaches DisconnectFromProviderAsync from two directions at once — the critical-error path and
    /// the orchestration loop unwinding — and a plain null check on SessionCts lets both through.
    /// </summary>
    public int ProviderDisconnectClaimed;

    /// <summary>
    /// Claim flag for session cleanup, taken with Interlocked. Cleanup is now reachable from two
    /// places — the orchestration loop's finally and ConnectAsync's — and must run exactly once.
    /// </summary>
    public int CleanupClaimed;

    /// <summary>
    /// Set when the engine itself decides to end the session, so teardown can report why instead of
    /// inferring it from the client socket's state. Null means the client ended it.
    /// </summary>
    public RealtimeAiSessionOutcome? TerminationCause { get; set; }

    // Negotiated once at connect (OutputModeNegotiator) and reused for the session — never re-sniffed.
    public RealtimeAiOutputMode OutputMode { get; set; }

    // Latency anchors — Stopwatch ticks, not wall clock: monotonic, immune to clock adjustment,
    // and allocation-free to read. Elapsed values are derived at the log site.
    public long SessionStartedAt { get; set; }
    public long ProviderConnectedAt { get; set; }
    public long TurnStartedAt { get; set; }

    /// <summary>Stopwatch stamp of the last frame from the provider; read with Interlocked because
    /// the observer polls it from its own task while the receive loop writes it.</summary>
    public long LastProviderMessageAt;
    public bool TurnFirstAudioReported { get; set; }

    // Runtime state
    public int Round { get; set; }
    public volatile bool IsAiSpeaking;
    public volatile bool IsClientAudioToProviderSuspended;

    /// <summary>
    /// Set when the provider reports the caller started speaking, cleared by the first thing the
    /// provider produces of its own. Between those two points the silence observer, the turn ceiling
    /// and the idle timer are all off, which is the one window on the phone path where a half-open
    /// socket goes completely unrecorded.
    /// </summary>
    public volatile bool IsAwaitingProviderResponse;
    public bool IsProviderResponseInProgress;
    public bool HasPendingProviderResponseTrigger;
    public bool CurrentResponseHasTextOutput;
    public bool CurrentResponseTextDoneHandled;
    public bool CurrentResponseProviderTurnCompleted;
    public bool CurrentResponseTtsSynthesisCompleted;
    public bool CurrentResponseTurnCompletedHandled;

    // Monotonic per-turn id (bumped when a new provider response starts). A TTS-synthesis watchdog
    // captures it when armed and compares on fire, so a watchdog from a superseded turn no-ops.
    public long CurrentTurnGeneration;

    /// <summary>
    /// The generation a watchdog force-completed, or -1. Kept separate from the external-TTS handled
    /// latch: a provider that recovers and sends response.done after the ceiling already closed the
    /// turn must not complete it a second time, and Round drives when the idle follow-up fires.
    /// </summary>
    public long ForceCompletedTurnGeneration = -1;

    /// <summary>
    /// The generation that completed on its own, or -1. Only the watchdogs consult it, so a turn that
    /// finished normally cannot then be force-completed a second time by its own ceiling. It
    /// deliberately does not gate one normal completion against another: whether a redundant
    /// response.done completes twice is separate, pinned behaviour.
    /// </summary>
    public long NormallyCompletedTurnGeneration = -1;

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
    /// <summary>
    /// Tools already answered once about a failure, and how many such answers this session has sent.
    ///
    /// <para>A plain HashSet where every other cross-thread field on this context is volatile,
    /// Interlocked or concurrent: these are touched only from <c>OnFunctionCallsReceivedAsync</c>, which
    /// runs on the provider receive loop and is awaited by it, so the whole dispatch is serial with
    /// respect to itself. If a function-call batch ever stops being awaited there, this needs revisiting
    /// with the rest of the loop.</para>
    /// </summary>
    public HashSet<string> FunctionCallFailuresAnswered { get; } = new();
    public int FunctionCallFailureRepliesSent { get; set; }

    public SemaphoreSlim WsSendLock { get; } = new(1, 1);
    public SemaphoreSlim ProviderResponseStateLock { get; } = new(1, 1);
    public SemaphoreSlim TurnCompletionStateLock { get; } = new(1, 1);

    // Actions exposed to consumer callbacks
    public RealtimeAiSessionActions SessionActions { get; set; }
}
