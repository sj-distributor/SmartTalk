using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class ReceivePhoneOrderRecordCommand : ICommand
{
    public string RecordName { get; set; }
    
    public byte[] RecordContent { get; set; }
}