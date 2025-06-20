using System.Net.WebSockets;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Messages.Commands.RealtimeAi;

public class RealtimeAiConnectCommand : ICommand
{
    public int AssistantId { get; set; }
    
    public WebSocket WebSocket { get; set; }
    
    public RealtimeAiAudioCodec InputFormat { get; set; }
    
    public RealtimeAiAudioCodec OutputFormat { get; set; }
}