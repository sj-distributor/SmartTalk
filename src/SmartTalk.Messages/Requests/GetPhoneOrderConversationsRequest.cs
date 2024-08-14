using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests;

public class GetPhoneOrderConversationsRequest : IRequest
{
    public int RecordId { get; set; }
}

public class GetPhoneOrderConversationsResponse : SmartTalkResponse<List<PhoneOrderConversationDto>>
{
}