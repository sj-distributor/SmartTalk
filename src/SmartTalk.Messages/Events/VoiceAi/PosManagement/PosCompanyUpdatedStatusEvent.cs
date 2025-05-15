using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Messages.Events.VoiceAi.PosManagement;

public class PosCompanyUpdatedStatusEvent : IEvent
{
    public PosCompanyDto Company { get; set; }
}