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

    // 代客致电等场景: 调用方 (如 Smarties KitchChat) 经 connect URL 的 ?instruction= 传入本通电话的系统指令/prompt。
    // 有值则用作本通对话指令 (覆盖 DB assistant prompt); 无值 = 照旧用 DB prompt (non-breaking)。
    public string Instruction { get; set; }

    public WebSocket TwilioWebSocket { get; set; }
    
    public PhoneOrderRecordType OrderRecordType { get; set; }
}