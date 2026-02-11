using System.Net.WebSockets;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public class RealtimeAiConnectionProfile
{
    public string ProfileId { get; set; }
}

/// <summary>
/// Model and session configuration prepared by the consumer.
/// All business-specific data fetching and prompt engineering should be done
/// before constructing this object — the generic V2 layer does not access
/// any data providers (e.g. IAiSpeechAssistantDataProvider).
/// </summary>
public class RealtimeAiModelConfig
{
    public AiSpeechAssistantProvider Provider { get; set; }

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

public class RealtimeSessionOptions
{
    public RealtimeAiModelConfig ModelConfig { get; set; }

    public RealtimeAiConnectionProfile ConnectionProfile { get; set; }

    public WebSocket WebSocket { get; set; }

    public RealtimeAiAudioCodec InputFormat { get; set; }

    public RealtimeAiAudioCodec OutputFormat { get; set; }

    public RealtimeAiServerRegion Region { get; set; }

    /// <summary>
    /// When true, the service buffers all audio (user + AI) and produces a WAV file on session end.
    /// </summary>
    public bool EnableRecording { get; set; }
}

public class RealtimeSessionCallbacks
{
    /// <summary>
    /// Called when the AI session is initialized and ready.
    /// The consumer receives a sendText delegate to send messages (e.g. greetings).
    /// </summary>
    public Func<Func<string, Task>, Task> OnSessionReadyAsync { get; set; }

    /// <summary>
    /// Called when audio data is received or sent.
    /// Parameters: (audioBytes, isAiOutput).
    /// </summary>
    public Func<byte[], bool, Task> OnAudioDataAsync { get; set; }

    /// <summary>
    /// Called when the session ends.
    /// Parameter: sessionId.
    /// </summary>
    public Func<string, Task> OnSessionEndedAsync { get; set; }

    /// <summary>
    /// Called when session ends with collected transcriptions.
    /// Parameters: (sessionId, transcriptions).
    /// </summary>
    public Func<string, IReadOnlyList<(AiSpeechAssistantSpeaker Speaker, string Text)>, Task> OnTranscriptionsReadyAsync { get; set; }

    /// <summary>
    /// Called when the session ends and recording is enabled.
    /// Parameters: (sessionId, wavBytes).
    /// </summary>
    public Func<string, byte[], Task> OnRecordingCompleteAsync { get; set; }

    /// <summary>
    /// Optional idle follow-up configuration. When set, AI will proactively
    /// send a follow-up message if the user stays silent after an AI turn.
    /// Null to disable.
    /// </summary>
    public RealtimeSessionIdleFollowUp IdleFollowUp { get; set; }
}

public class RealtimeSessionIdleFollowUp
{
    /// <summary>
    /// Seconds of user silence before the AI sends a follow-up message.
    /// </summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// The message AI will say when the user has been silent too long.
    /// </summary>
    public string FollowUpMessage { get; set; }

    /// <summary>
    /// Number of AI turns to skip before enabling idle follow-up.
    /// Null means enable from the first turn.
    /// </summary>
    public int? SkipRounds { get; set; }
}