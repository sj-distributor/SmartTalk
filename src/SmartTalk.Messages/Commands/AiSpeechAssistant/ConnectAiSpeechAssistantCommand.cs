using System.Net.WebSockets;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class ConnectAiSpeechAssistantCommand : ICommand
{
    public string From { get; set; }
    
    public string To { get; set; }
    
    public string Host { get; set; }
    
    public int? NumberId { get; set; }
    
    public int? AssistantId { get; set; }

    // 调用方传入的完整本通指令; 有值时沿用原逻辑覆盖 Assistant knowledge prompt。
    public string Instruction { get; set; }

    // 调用方通过 URL path 传入的 URL-safe Base64 动态问题，由 AiSpeechAssistantService 解析。
    public string EncodedQuestion { get; set; }

    // 代客致电等场景: 调用方传入本通动态问题, 由 Assistant knowledge prompt 的 #{question} 占位符承接。
    public string Question { get; set; }

    public WebSocket TwilioWebSocket { get; set; }
    
    public PhoneOrderRecordType OrderRecordType { get; set; }
}