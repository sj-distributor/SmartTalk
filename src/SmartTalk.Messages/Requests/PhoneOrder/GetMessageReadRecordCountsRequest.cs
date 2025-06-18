using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetMessageReadRecordCountsRequest : IRequest
{
}

public class GetMessageReadRecordCountsResponse : SmartTalkResponse<int>
{
}