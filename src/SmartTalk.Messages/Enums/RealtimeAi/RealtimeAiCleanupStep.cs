namespace SmartTalk.Messages.Enums.RealtimeAi;

/// <summary>
/// The steps session teardown runs, each isolated so one failure cannot take the others down.
///
/// <para>An enum rather than the prose strings this used to carry: the step name is what an operator
/// facets on to ask "which teardown step is failing, and how often". Free text like
/// "invoke OnSessionEndedAsync" cannot be grouped, and a typo silently creates a second bucket.</para>
/// </summary>
public enum RealtimeAiCleanupStep
{
    AcknowledgeClientClose,
    CloseClientSocket,
    DisconnectProvider,
    StopIdleTimer,
    InvokeSessionEnded,
    HandleRecording,
    HandleTranscriptions
}
