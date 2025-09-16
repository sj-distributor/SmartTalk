using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Messages.Events.Pos;

public class PosCompanyUpdatedEvent : IEvent
{
    public PosCompanyDto Company { get; set; }
}