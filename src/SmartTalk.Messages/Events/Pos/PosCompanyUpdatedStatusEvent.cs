using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Messages.Events.Pos;

public class PosCompanyUpdatedStatusEvent : IEvent
{
    public CompanyDto Company { get; set; }
}