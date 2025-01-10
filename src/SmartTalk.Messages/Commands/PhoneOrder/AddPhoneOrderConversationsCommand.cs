using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class AddPhoneOrderConversationsCommand : ICommand
{
    public List<PhoneCallConversationDto> Conversations { get; set; }
}

public class AddPhoneOrderConversationsResponse : SmartTalkResponse<List<PhoneCallConversationDto>>
{
}