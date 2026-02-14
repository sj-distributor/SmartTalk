using System.Net.WebSockets;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2;

public class RealtimeAiConnectionProfile
{
    public string ProfileId { get; set; }
}

public class RealtimeAiClientConfig
{
    public RealtimeAiClient Client { get; set; }
}

/// <summary>
/// Model and session configuration prepared by the consumer.
/// All business-specific data fetching and prompt engineering should be done
/// before constructing this object — the generic V2 layer does not access
/// any data providers (e.g. IAiSpeechAssistantDataProvider).
/// </summary>
public class RealtimeAiModelConfig
{
    public RealtimeAiProvider Provider { get; set; }

    public string ServiceUrl { get; set; }

    public string Voice { get; set; }

    public string ModelName { get; set; }

    public string ModelLanguage { get; set; }

    /// <summary>
    /// The final system prompt with all variables already resolved by the consumer.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    /// Provider-specific tool/function-call definitions, passed through as-is to the provider payload.
    /// The JSON schema differs per provider, so these are kept as opaque objects:
    /// <list type="bullet">
    ///   <item>OpenAI: <c>{"type":"function","name":"...","description":"...","parameters":{...}}</c></item>
    ///   <item>Google: <c>{"functionDeclarations":[{"name":"...","description":"...","parameters":{...}}]}</c></item>
    /// </list>
    /// </summary>
    public List<object> Tools { get; set; } = new();

    /// <summary>
    /// Provider-specific turn detection / voice activity detection configuration.
    /// <list type="bullet">
    ///   <item>OpenAI: e.g. <c>{"type":"server_vad"}</c>  (defaults to server_vad if null)</item>
    ///   <item>Google: passed as <c>realtimeInputConfig</c></item>
    /// </list>
    /// </summary>
    public object TurnDetection { get; set; }

    /// <summary>
    /// OpenAI-specific input audio noise reduction configuration. Ignored by other providers.
    /// </summary>
    public object InputAudioNoiseReduction { get; set; }
}

/// <summary>
/// Exposes session-level operations to consumer callbacks.
/// New capabilities can be added here without changing callback signatures.
/// </summary>
public class RealtimeAiSessionActions
{
    /// <summary>
    /// Send audio (base64 payload) directly to the client (e.g. play a pre-recorded message).
    /// </summary>
    public Func<string, Task> SendAudioToClientAsync { get; init; }

    /// <summary>
    /// Send text to the AI provider as a user message and trigger an AI response.
    /// </summary>
    public Func<string, Task> SendTextToProviderAsync { get; init; }

    /// <summary>
    /// Suspend forwarding client audio to the AI provider.
    /// Use when the consumer needs exclusive control of the audio channel
    /// (e.g. playing a pre-recorded message to the client without the provider hearing user noise).
    /// Client audio will still be received from the WebSocket but will not be sent to the provider.
    /// </summary>
    public Action SuspendClientAudioToProvider { get; init; }

    /// <summary>
    /// Resume forwarding client audio to the AI provider after a previous <see cref="SuspendClientAudioToProvider"/>.
    /// </summary>
    public Action ResumeClientAudioToProvider { get; init; }
}

public class RealtimeSessionOptions
{
    // --- Configuration ---

    public RealtimeAiClientConfig ClientConfig { get; set; }
    
    public RealtimeAiModelConfig ModelConfig { get; set; }

    public RealtimeAiConnectionProfile ConnectionProfile { get; set; }

    public WebSocket WebSocket { get; set; }

    public RealtimeAiServerRegion Region { get; set; }

    /// <summary>
    /// When true, the service buffers all audio (user + AI) and produces a WAV file on session end.
    /// </summary>
    public bool EnableRecording { get; set; }

    /// <summary>
    /// Optional idle follow-up configuration. When set, AI will proactively
    /// send a follow-up message if the user stays silent after an AI turn.
    /// Null to disable.
    /// </summary>
    public RealtimeSessionIdleFollowUp IdleFollowUp { get; set; }

    // --- Callbacks ---

    /// <summary>
    /// Called when the AI session is initialized and ready.
    /// The consumer receives <see cref="RealtimeAiSessionActions"/> to interact with the session (e.g. send a greeting).
    /// </summary>
    public Func<RealtimeAiSessionActions, Task> OnSessionReadyAsync { get; set; }

    /// <summary>
    /// Called when the AI provider suggests a function call (e.g. from OpenAI response.done).
    /// Parameters: (functionCallData, sessionActions).
    /// The consumer can use <see cref="RealtimeAiSessionActions"/> to send audio to the client,
    /// send text to the provider, etc.
    /// Return a <see cref="RealtimeAiFunctionCallResult"/> with Output to continue the conversation,
    /// or null if no reply is needed. Null callback to ignore function calls.
    /// </summary>
    public Func<RealtimeAiWssFunctionCallData, RealtimeAiSessionActions, Task<RealtimeAiFunctionCallResult>> OnFunctionCallAsync { get; set; }

    /// <summary>
    /// Called when the client sends a "start" lifecycle event.
    /// Parameters: (sessionId, metadata dictionary with keys like "callSid", "streamSid").
    /// </summary>
    public Func<string, Dictionary<string, string>, Task> OnClientStartAsync { get; set; }

    /// <summary>
    /// Called when the client sends a "stop" lifecycle event (e.g. Twilio stream stop).
    /// Parameter: sessionId.
    /// </summary>
    public Func<string, Task> OnClientStopAsync { get; set; }

    /// <summary>
    /// Called when the session ends.
    /// Parameter: sessionId.
    /// </summary>
    public Func<string, Task> OnSessionEndedAsync { get; set; }

    /// <summary>
    /// Called when session ends with collected transcriptions.
    /// Parameters: (sessionId, transcriptions).
    /// </summary>
    public Func<string, IReadOnlyList<(AiSpeechAssistantSpeaker Speaker, string Text)>, Task> OnTranscriptionsCompletedAsync { get; set; }

    /// <summary>
    /// Called when the session ends and recording is enabled.
    /// Parameters: (sessionId, wavBytes).
    /// </summary>
    public Func<string, byte[], Task> OnRecordingCompleteAsync { get; set; }
}

public class RealtimeSessionIdleFollowUp
{
    /// <summary>
    /// Seconds of user silence before the AI sends a follow-up message.
    /// </summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// The message AI will say when the user has been silent too long.
    /// Can be null when only OnTimeoutAsync is needed.
    /// </summary>
    public string FollowUpMessage { get; set; }

    /// <summary>
    /// Optional async action invoked when idle timeout fires.
    /// Called after FollowUpMessage is sent (if any).
    /// </summary>
    public Func<Task> OnTimeoutAsync { get; set; }

    /// <summary>
    /// Number of AI turns to skip before enabling idle follow-up.
    /// Null means enable from the first turn.
    /// </summary>
    public int? SkipRounds { get; set; }
}