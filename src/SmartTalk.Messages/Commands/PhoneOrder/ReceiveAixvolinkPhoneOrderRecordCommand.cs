using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class ReceiveAixvolinkPhoneOrderRecordCommand : ICommand
{
    public string RecordingUrl { get; set; }

    public DateTimeOffset CallTime { get; set; }

    public string CallerNumber { get; set; }

    public string CalleeNumber { get; set; }

    public int AgentId { get; set; }

    public int? AssistantId { get; set; }

    public PhoneOrderRecordType OrderRecordType { get; set; } = PhoneOrderRecordType.InBound;
}
