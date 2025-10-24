using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Events.HrInterView;

public class ConnectWebSocketEvent : IEvent
{
    public Guid SessionId { get; set; }
}