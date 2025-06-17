using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderRecordsUnreadCountsRequest : IRequest
{
}

public class GetPhoneOrderRecordsUnreadCountsResponse : SmartTalkResponse<int>
{
}