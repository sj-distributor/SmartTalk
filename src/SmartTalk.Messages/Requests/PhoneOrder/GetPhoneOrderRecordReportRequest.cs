using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderRecordReportRequest : IRequest
{
    public string CallSid { get; set; }
    
    public SystemLanguage Language { get; set; }
}

public class GetPhoneOrderRecordReportResponse : SmartTalkResponse<PhoneOrderRecordReportDto>
{
}