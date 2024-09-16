using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class AddManualOrderCommand : ICommand
{
    public long OrderId { get; set; }
    
    public int RecordId { get; set; }

    public PhoneOrderRestaurant Restaurant { get; set; }
}

public class AddManualOrderResponse : SmartTalkResponse<List<PhoneOrderOrderItemDto>>
{
}