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
}

public class RealtimeAiErrorData
{
    public string Code { get; set; } // 错误码 (Error code)
    public string Message { get; set; } // 错误信息 (Error message)
    public bool IsCritical { get; set; } // 是否是严重错误，可能导致会话终止 (Whether it is a critical error, may cause session termination)
}