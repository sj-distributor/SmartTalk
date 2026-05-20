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
    TranscriptionLanguage,

    /// <summary>
    /// Optional per-assistant override of the OpenAI transcription model. Row
    /// content is a JSON object with a single <c>model</c> property. Example:
    /// <c>{ "model": "whisper-1" }</c> (downgrade) or
    /// <c>{ "model": "gpt-4o-mini-transcribe" }</c> (cheaper variant).
    /// Absent / inactive row → adapter falls back to its compile-time default
    /// (<c>gpt-4o-transcribe</c>, OpenAI's most capable transcription model).
    /// Unrecognised values are passed through verbatim — OpenAI will reject
    /// them server-side rather than the adapter silently substituting.
    /// </summary>
    TranscriptionModel
}