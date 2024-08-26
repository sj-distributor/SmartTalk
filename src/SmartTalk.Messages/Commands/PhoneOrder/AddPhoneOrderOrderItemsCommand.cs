using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class AddPhoneOrderOrderItemsCommand : ICommand
{
    public List<PhoneOrderOrderItemDto> OrderItems { get; set; }
}

public class AddPhoneOrderOrderItemsResponse : SmartTalkResponse<List<PhoneOrderOrderItemDto>>
{
}