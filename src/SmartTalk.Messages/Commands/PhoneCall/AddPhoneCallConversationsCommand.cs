using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneCall;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.PhoneCall;

public class AddPhoneCallConversationsCommand : ICommand
{
    public List<PhoneCallConversationDto> Conversations { get; set; }
}

public class AddPhoneOrderConversationsResponse : SmartTalkResponse<List<PhoneCallConversationDto>>
{
}