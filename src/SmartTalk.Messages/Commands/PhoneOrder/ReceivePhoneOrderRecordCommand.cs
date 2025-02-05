using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class ReceivePhoneOrderRecordCommand : ICommand
{
    public string RecordName { get; set; }
    
    public byte[] RecordContent { get; set; }
    
    public string RecordUrl { get; set; }
    
    public DateTimeOffset? CreatedDate { get; set; }
    
    public int AgentId { get; set; }
}