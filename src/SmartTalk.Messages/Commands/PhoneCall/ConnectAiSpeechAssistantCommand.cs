using System.Net.WebSockets;
using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.PhoneCall;

public class ConnectAiSpeechAssistantCommand : ICommand
{
    public string From { get; set; }
    
    public string To { get; set; }
    
    public WebSocket TwilioWebSocket { get; set; }
}