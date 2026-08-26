using System.Net.WebSockets;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
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

    // URL-safe Base64 编码的 JSON prompt variables，由 AiSpeechAssistantService 在连接入口统一解析。
    public string EncodedPromptVariables { get; set; }

    // 仅兼容已发布的 /question/{value} 路由；内容是 URL-safe Base64 编码的纯文本，不参与 JSON 格式推断。
    public string EncodedLegacyQuestion { get; set; }

    // 内部调用方可直接提供已解析变量；key 对应 Assistant prompt 中的 #{key} 占位符。
    public Dictionary<string, string> PromptVariables { get; set; }

    public AiSpeechAssistantConnectionMode ConnectionMode { get; set; }

    public WebSocket TwilioWebSocket { get; set; }
    
    public PhoneOrderRecordType OrderRecordType { get; set; }
}