using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class ReceivePhoneOrderRecordCommand : ICommand
{
    public string RecordName { get; set; }
    
    public byte[] RecordContent { get; set; }

    public string Restaurant { get; set; }
}