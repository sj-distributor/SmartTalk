using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class AddPhoneOrderConversationsCommand : ICommand
{
    public List<PhoneOrderConversationDto> Conversations { get; set; }
}

public class AddPhoneOrderConversationsResponse : SmartTalkResponse<AddPhoneOrderConversationsResponseData>
{
}

public class AddPhoneOrderConversationsResponseData
{
    public List<PhoneOrderConversationDto> Conversations { get; set; }
    
    public PhoneOrderDetailDto PhoneOrderDetail { get; set; }
}