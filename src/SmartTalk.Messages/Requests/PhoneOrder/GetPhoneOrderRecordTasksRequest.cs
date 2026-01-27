using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderRecordTasksRequest : HasServiceProviderId, IRequest
{
}

public class GetPhoneOrderRecordTasksResponse : SmartTalkResponse<List<WaitingProcessingEventsDto>>
{
}