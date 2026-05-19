namespace SmartTalk.Messages.Enums.AiSpeechAssistant;

public enum AiSpeechAssistantSessionConfigType
{
    Tool,
    TurnDirection,
    InputAudioNoiseReduction,

    /// <summary>
    /// Optional hint for OpenAI's transcription model. Row content is a JSON
    /// object with a single <c>language</c> property — ISO-639-1 code or
    /// <c>"yue"</c> for Cantonese. Example:
    /// <c>{ "language": "yue" }</c>.
    /// Absent / inactive row → no hint sent (current default behaviour).
    /// </summary>
    TranscriptionLanguage
}