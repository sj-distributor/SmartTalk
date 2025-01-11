using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneCall;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneCall;

public class GetPhoneCallConversationsRequest : IRequest
{
    public int RecordId { get; set; }
}

public class GetPhoneCallConversationsResponse : SmartTalkResponse<List<PhoneCallConversationDto>>
{
}