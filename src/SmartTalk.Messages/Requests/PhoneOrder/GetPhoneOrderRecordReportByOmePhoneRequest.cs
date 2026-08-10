using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderRecordReportByOmePhoneRequest : IRequest
{
    public DateTimeOffset CallTime { get; set; }

    public string CallerNumber { get; set; }

    public string CalleeNumber { get; set; }
    
    public string TransferCallNumber { get; set; }
    
    public SystemLanguage Language { get; set; }
}

public class GetPhoneOrderRecordReportByOmePhoneResponse : SmartTalkResponse<PhoneOrderRecordReportDto>
{
}