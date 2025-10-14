using System.Net.WebSockets;
using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.HrInterView;

public class ConnectHrInterViewCommand : ICommand
{
    public string Host { get; set; }
    
    public Guid SessionId { get; set; } 
    
    public WebSocket WebSocket { get; set; }
}