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
    
    public WebSocket TwilioWebSocket { get; set; }
    
    public PhoneOrderRecordType OrderRecordType { get; set; }
}