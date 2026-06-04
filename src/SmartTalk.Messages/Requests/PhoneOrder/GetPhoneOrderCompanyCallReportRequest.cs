using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderCompanyCallReportRequest : IRequest
{
    public DateTime StartDate { get; set; }
    
    public DateTime EndDate { get; set; }
}

public class GetPhoneOrderCompanyCallReportResponse : SmartTalkResponse<string>;
