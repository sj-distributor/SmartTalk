using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.Restaurant;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneOrderRecordInformationDto
{
    public AgentDto Agent { get; set; }
    
    public DateTimeOffset StartDate { get; set; }
}