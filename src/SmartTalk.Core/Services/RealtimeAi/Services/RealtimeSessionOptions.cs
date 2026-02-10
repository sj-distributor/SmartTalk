using System.Net.WebSockets;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAi.Services;

public class RealtimeSessionOptions
{
    public WebSocket WebSocket { get; set; }

    public Domain.AISpeechAssistant.AiSpeechAssistant AssistantProfile { get; set; }

    public string InitialPrompt { get; set; }

    public RealtimeAiAudioCodec InputFormat { get; set; }

    public RealtimeAiAudioCodec OutputFormat { get; set; }

    public RealtimeAiServerRegion Region { get; set; }
}

public class RealtimeSessionCallbacks
{
    /// <summary>
    /// Called after recording WAV is uploaded. Parameters: (fileUrl, sessionId).
    /// Return without action if recording post-processing is not needed.
    /// </summary>
    public Func<string, string, Task> OnRecordingSavedAsync { get; set; }

    /// <summary>
    /// Called when session ends with collected transcriptions.
    /// Parameters: (sessionId, transcriptions).
    /// </summary>
    public Func<string, IReadOnlyList<(AiSpeechAssistantSpeaker Speaker, string Text)>, Task> OnTranscriptionsReadyAsync { get; set; }

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