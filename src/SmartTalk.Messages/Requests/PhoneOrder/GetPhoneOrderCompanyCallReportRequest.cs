using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderCompanyCallReportRequest : IRequest
{
    public PhoneOrderCallReportType ReportType { get; set; }
}

public class GetPhoneOrderCompanyCallReportResponse : SmartTalkResponse<string>;
