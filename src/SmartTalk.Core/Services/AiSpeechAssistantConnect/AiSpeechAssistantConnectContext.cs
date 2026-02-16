using System.Net.WebSockets;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public class AiSpeechAssistantConnectContext
{
    // Call identity
    public string CallSid { get; set; }
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

    // Assistant & knowledge
    public AiSpeechAssistantDto Assistant { get; set; }
    public AiSpeechAssistantKnowledgeDto Knowledge { get; set; }

    // Routing
    public int? ForwardAssistantId { get; set; }
    public string HumanContactPhone { get; set; }
    public string TransferCallNumber { get; set; }

    // Service hours
    public bool IsInAiServiceHours { get; set; } = true;
    public bool IsEnableManualService { get; set; }

    // Call state
    public bool IsTransfer { get; set; }
    public AiSpeechAssistantOrderDto OrderItems { get; set; }
    public AiSpeechAssistantUserInfoDto UserInfo { get; set; }
    public AiSpeechAssistantUserInfoDto LastUserInfo { get; set; }
}
