using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Events.PhoneOrder;

public class PhoneOrderRecordUpdatedEvent : IEvent
{
    public int RecordId { get; set; }
    
    public string UserName { get; set; }

    public DialogueScenarios DialogueScenarios { get; set; }
    
    public DialogueScenarios? OriginalScenarios { get; set; }
}