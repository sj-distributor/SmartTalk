using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Messages.Events.Pos;

public class PosCompanyDeletedEvent : IEvent
{
    public PosCompanyDto Company { get; set; }
}