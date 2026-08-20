using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class TranscribeAndAnalyzePhoneOrderRecordCommand : ICommand
{
    public string RecordingUrl { get; set; }

    public DateTimeOffset? CallTime { get; set; }

    public string CallerNumber { get; set; }

    public string CalleeNumber { get; set; }

    public int AgentId { get; set; }

    public int? AssistantId { get; set; }

    public PhoneOrderRecordType OrderRecordType { get; set; } = PhoneOrderRecordType.InBound;
}

public class TranscribeAndAnalyzePhoneOrderRecordResponse : SmartTalkResponse<TranscribeAndAnalyzePhoneOrderRecordResponseData>
{
}

public class TranscribeAndAnalyzePhoneOrderRecordResponseData
{
    public string RecordingUrl { get; set; }

    public string Status { get; set; }
}
