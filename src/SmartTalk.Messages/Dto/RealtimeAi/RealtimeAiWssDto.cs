using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Messages.Dto.RealtimeAi;

public class RealtimeAiWssAudioData
{
    public string Base64Payload { get; set; }
    public string ItemId { get; set; }
    
    public Dictionary<string, object> CustomProperties { get; set; } = new();
}

public class RealtimeAiWssTranscriptionData
{
    public string Transcript { get; set; }
    
    public AiSpeechAssistantSpeaker Speaker { get; set; }
    
    public string ItemId { get; set; }
}

public class RealtimeAiWssFunctionCallData
{
    public string CallId { get; set; }

    public string FunctionName { get; set; }

    public string ArgumentsJson { get; set; }
}

public class RealtimeAiFunctionCallResult
{
    /// <summary>
    /// The function execution output to send back to the AI provider.
    /// </summary>
    public string Output { get; set; }

    /// <summary>
    /// When true, sends response.create even if Output is empty (no function_call_output sent).
    /// Used for scenarios like transfer calls where the AI should speak without explicit output text.
    /// </summary>
    public bool ShouldTriggerResponse { get; set; }
}

public class ParsedRealtimeAiProviderEvent
{
    public RealtimeAiWssEventType Type { get; set; }

    public object Data { get; set; }

    public string RawJson { get; set; }

    public string ItemId { get; set; }

    /// <summary>
    /// Token usage for the AI turn, populated by the provider adapter when the
    /// provider emits usage data alongside the turn-completion event (currently
    /// OpenAI's <c>response.done.response.usage</c>). <c>null</c> when the
    /// underlying message has no usage block (older provider snapshots) or when
    /// the event type does not carry usage. Sits on the event rather than on
    /// <see cref="Data"/> so it can coexist with function-call payloads.
    /// </summary>
    public RealtimeAiWssUsageData Usage { get; set; }
}

/// <summary>
/// Per-turn token-usage breakdown reported by the AI provider on turn completion.
/// All fields nullable because providers may omit individual sub-counts; consumers
/// should treat missing values as "not reported" rather than zero.
/// </summary>
public class RealtimeAiWssUsageData
{
    /// <summary>Total tokens consumed for this turn (input + output).</summary>
    public int? TotalTokens { get; set; }

    /// <summary>Tokens consumed from the prompt / conversation history.</summary>
    public int? InputTokens { get; set; }

    /// <summary>Tokens produced by the AI in this turn.</summary>
    public int? OutputTokens { get; set; }

    /// <summary>Subset of input tokens served from the provider's prompt cache.</summary>
    public int? CachedTokens { get; set; }

    /// <summary>Subset of input tokens classified as audio (rather than text).</summary>
    public int? InputAudioTokens { get; set; }

    /// <summary>Subset of input tokens classified as text.</summary>
    public int? InputTextTokens { get; set; }

    /// <summary>Subset of output tokens classified as audio (rather than text).</summary>
    public int? OutputAudioTokens { get; set; }

    /// <summary>Subset of output tokens classified as text.</summary>
    public int? OutputTextTokens { get; set; }
}

public class RealtimeAiErrorData
{
    public string Code { get; set; } // 错误码 (Error code)
    public string Message { get; set; } // 错误信息 (Error message)
    public bool IsCritical { get; set; } // 是否是严重错误，可能导致会话终止 (Whether it is a critical error, may cause session termination)
}