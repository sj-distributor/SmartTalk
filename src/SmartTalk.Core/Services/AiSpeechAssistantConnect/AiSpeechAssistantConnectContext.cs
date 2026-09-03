using System.Net.WebSockets;
using SmartTalk.Core.Logging;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public class AiSpeechAssistantConnectContext
{
    // Call identity
    public string SessionId { get; set; }

    /// <summary>
    /// Ambient log scope for this call, opened at the entry point. CallSid and StreamSid are set on
    /// it once Twilio's start frame arrives, which is why it must be deferred rather than a plain
    /// LogContext.PushProperty.
    /// </summary>
    public DeferredLogScope LogScope { get; set; }

    public string CallSid { get; set; }

    /// <summary>
    /// Tools that have already spent their one reply for unreadable arguments this call. Bounds the
    /// recovery so a model re-emitting the same malformed payload cannot keep completing turns, which
    /// would restart the idle countdown forever on a path that has no session ceiling.
    /// </summary>
    public HashSet<string> ArgumentRecoveryClaims { get; } = new();
    public string StreamSid { get; set; }
    public string Host { get; set; }
    public string From { get; set; }
    public string To { get; set; }

    // Command
    public int? AssistantId { get; set; }
    public int? NumberId { get; set; }
    public int AgentId { get; set; }
    public WebSocket TwilioWebSocket { get; set; }
    public PhoneOrderRecordType OrderRecordType { get; set; }

    // 代客致电等场景: 调用方经 connect URL ?instruction= 传入的本通指令; 有值则覆盖 DB prompt (non-breaking)。
    public string Instruction { get; set; }

    // Assistant & knowledge
    public string Prompt { get; set; }
    public string UserProfileJson { get; set; }
    public AiSpeechAssistantDto Assistant { get; set; }
    public AiSpeechAssistantKnowledgeDto Knowledge { get; set; }
    public AiSpeechAssistantTimer Timer { get; set; }
    public List<AiSpeechAssistantFunctionCall> FunctionCalls { get; set; }

    // Routing
    public int? ForwardAssistantId { get; set; }
    public string HumanContactPhone { get; set; }
    public string TransferCallNumber { get; set; }
    public List<AgentTransferCallConfig> AgentTransferCallConfigs { get; set; }
    public TimeZoneInfo TimeZone { get; set; }

    // Service hours
    public bool IsInAiServiceHours { get; set; } = true;
    public bool IsEnableManualService { get; set; }

    // Call state
    public bool IsTransfer { get; set; }
    public AiSpeechAssistantOrderDto OrderItems { get; set; }
    public AiSpeechAssistantComplaintInfoDto ComplaintInfo { get; set; } = new();
    public AiSpeechAssistantUserInfoDto UserInfo { get; set; }
    public AiSpeechAssistantUserInfoDto LastUserInfo { get; set; }
}
