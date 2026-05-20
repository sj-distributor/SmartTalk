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
    TranscriptionModel,

    /// <summary>
    /// Optional per-assistant cap on the response output token count. Row
    /// content is a JSON object with a single <c>value</c> property holding a
    /// positive integer. Example: <c>{ "value": 1200 }</c>. Absent / inactive
    /// row → no <c>max_response_output_tokens</c> field is sent, so OpenAI
    /// uses its server-side default (effectively unlimited within the session
    /// budget). Caps a single AI turn — useful when an assistant occasionally
    /// monologues and delays the user's next prompt.
    /// </summary>
    MaxResponseOutputTokens,

    /// <summary>
    /// Optional per-assistant playback-speed multiplier for the AI's audio
    /// output. Row content is a JSON object with a single <c>value</c>
    /// property holding a decimal in the range supported by OpenAI (currently
    /// 0.25 – 1.5; 1.0 = natural). Example: <c>{ "value": 0.9 }</c> for
    /// elderly customers, <c>{ "value": 1.1 }</c> for fast-paced ones.
    /// Absent / inactive row → no <c>speed</c> field is sent, so OpenAI uses
    /// 1.0 (current behaviour).
    /// </summary>
    OutputAudioSpeed
}