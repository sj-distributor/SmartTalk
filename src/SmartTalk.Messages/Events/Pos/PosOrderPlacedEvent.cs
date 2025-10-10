using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Messages.Events.Pos;

public class PosOrderPlacedEvent : IEvent
{
    public PosOrderDto Order { get; set; }
}