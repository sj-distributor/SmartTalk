using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands;

public class AddPhoneOrderConversationsCommand : ICommand
{
    public List<PhoneOrderConversationDto> Conversations { get; set; }
}

public class AddPhoneOrderConversationsResponse : SmartTalkResponse<List<PhoneOrderConversationDto>>
{
}