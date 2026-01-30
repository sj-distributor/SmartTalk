using System.Net.WebSockets;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Messages.Commands.RealtimeAi;

public class RealtimeAiConnectCommand : ICommand
{
    public int AssistantId { get; set; }
    
    public WebSocket WebSocket { get; set; }
    
    public RealtimeAiAudioCodec InputFormat { get; set; }
    
    public RealtimeAiAudioCodec OutputFormat { get; set; }
    
    public RealtimeAiServerRegion Region { get; set; }
    
    public PhoneOrderRecordType OrderRecordType { get; set; }
}