using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderRecordReportRequest : IRequest
{
    public string CallSid { get; set; }
    
    public int Language { get; set; }
}

public class GetPhoneOrderRecordReportResponse : SmartTalkResponse<PhoneOrderRecordReportDto>
{
}