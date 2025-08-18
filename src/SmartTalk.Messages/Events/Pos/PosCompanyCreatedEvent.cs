using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Messages.Events.Pos;

public class PosCompanyCreatedEvent : IEvent
{
    public PosCompanyDto Company { get; set; }
}